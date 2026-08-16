package fileapi

import (
	"archive/zip"
	"bufio"
	"fmt"
	"io"
	"mime"
	"net/http"
	"os"
	"path"
	"path/filepath"
	"regexp"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/api/utils"
	"github.com/kalinkasolutions/FileHub/backend/config"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
	"github.com/kalinkasolutions/FileHub/backend/services/shareservice"
)

type FileApi struct {
	logger            logger.ILogger
	config            config.Config
	publicPathService publicpathservice.IPublicPathService
	shareService      shareservice.IShareService
}

type NavigateParams struct {
	Id   int    `json:"Id"`
	Path string `json:"Path"`
}

func Register(router *gin.Engine, logger logger.ILogger, config config.Config, publicPathService publicpathservice.IPublicPathService, shareService shareservice.IShareService) {
	fa := &FileApi{
		logger:            logger,
		config:            config,
		publicPathService: publicPathService,
		shareService:      shareService,
	}

	router.GET("api/files", fa.getFileList)
	router.POST("api/files/navigate", fa.navigate)
	router.GET("api/files/download/:id/*path", fa.download)

	router.GET("public-api/files/download/:id", fa.downloadPublicShare)
}

func (fa *FileApi) getFileList(ctx *gin.Context) {
	publicPaths, err := fa.publicPathService.GetBasePaths()

	if err != nil {
		ctx.JSON(http.StatusBadRequest, gin.H{"error": "failed to load path"})
		return
	}

	ctx.JSON(http.StatusOK, publicPaths)
}

func (fa *FileApi) navigate(ctx *gin.Context) {
	var req NavigateParams
	err := ctx.ShouldBindJSON(&req)

	if err != nil {
		ctx.JSON(http.StatusBadRequest, gin.H{"error": "invalid request"})
		return
	}

	navigationName, navigation, err := fa.publicPathService.GetNavigationPaths(req.Id, req.Path)

	if err != nil {
		ctx.JSON(http.StatusBadRequest, gin.H{"error": "failed to load path"})
		return
	}

	ctx.JSON(http.StatusOK, gin.H{
		"NavigationName": navigationName,
		"Entries":        navigation,
	})
}

func (fa *FileApi) downloadPublicShare(ctx *gin.Context) {
	id := ctx.Param("id")

	share, err := fa.shareService.GetShareById(id)

	if err != nil {
		ctx.Redirect(http.StatusFound, utils.RedirectUri(fa.config))
		return
	}

	_, err = os.Stat(share.Path)

	if err != nil {
		fa.logger.Error("failed to read filestats for path: %s, %v", share.Path, err)
		ctx.Redirect(http.StatusFound, utils.RedirectUri(fa.config))
		return
	}

	fa.shareService.UpdateDownloadCount(share.Id)

	fa.handleFileOrDirectroyDownload(ctx, share.Path)
}

func (fa *FileApi) download(ctx *gin.Context) {
	validatedFilePath, success := utils.TryGetValidatedPathFromParam(ctx, fa.publicPathService)

	if !success {
		return
	}

	fa.handleFileOrDirectroyDownload(ctx, validatedFilePath)
}

func (fa *FileApi) handleFileOrDirectroyDownload(ctx *gin.Context, path string) {
	fileStats, err := os.Stat(path)

	if err != nil {
		fa.logger.Error("failed to read filestats for path: %s, %v", path, err)
		ctx.Redirect(http.StatusFound, utils.RedirectUri(fa.config))
		return
	}

	if fileStats.IsDir() {
		fa.downloadDirectoryAsZip(ctx, path)
		return
	}

	fa.downloadFile(ctx, fileStats, path)
}

func (fa *FileApi) downloadFile(ctx *gin.Context, fileStats os.FileInfo, path string) {
	ctx.Header("Content-Disposition", attachmentHeader(fileStats.Name()))
	ctx.Header("Content-Type", "application/octet-stream")
	ctx.Header("Content-Length", fmt.Sprintf("%d", fileStats.Size()))
	ctx.File(path)
}

// attachmentHeader quotes the filename properly. Building this header by hand breaks on
// names containing a double quote, which truncates the name the browser saves.
func attachmentHeader(fileName string) string {
	return mime.FormatMediaType("attachment", map[string]string{"filename": fileName})
}

var unsafeFileNameChars = regexp.MustCompile(`[^a-zA-Z\d\s\.\-\_\(\)]`)

func (fa *FileApi) downloadDirectoryAsZip(ctx *gin.Context, validatedFilePath string) {
	zipName := unsafeFileNameChars.ReplaceAllString(path.Base(validatedFilePath), "")

	ctx.Header("Content-Type", "application/zip")
	ctx.Header("Content-Disposition", attachmentHeader(zipName))
	ctx.Status(http.StatusOK)

	zipWriter := zip.NewWriter(bufio.NewWriter(ctx.Writer))
	defer zipWriter.Close()

	err := filepath.Walk(validatedFilePath, func(entryPath string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}

		return addToZip(zipWriter, validatedFilePath, entryPath, info)
	})

	if err != nil {
		fa.logger.Warning("Aborted creating zip for path: %s", validatedFilePath)
	}
}

func addToZip(zipWriter *zip.Writer, rootPath string, entryPath string, info os.FileInfo) error {
	relPath, err := filepath.Rel(rootPath, entryPath)

	if err != nil {
		return err
	}

	header, err := zip.FileInfoHeader(info)

	if err != nil {
		return err
	}

	header.Name = relPath

	if info.IsDir() {
		header.Name += "/"
	} else {
		header.Method = zip.Store
	}

	writer, err := zipWriter.CreateHeader(header)

	if err != nil {
		return err
	}

	if info.IsDir() {
		return nil
	}

	file, err := os.Open(entryPath)

	if err != nil {
		return err
	}

	defer file.Close()

	_, err = io.Copy(writer, file)
	return err
}
