package fileapi

import (
	"archive/zip"
	"bufio"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/api/utils"
	"github.com/kalinkasolutions/FileHub/backend/config"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
	"github.com/kalinkasolutions/FileHub/backend/services/shareservice"
)

type FileApi struct {
	router            *gin.Engine
	logger            logger.ILogger
	config            config.Config
	publicPathService publicpathservice.IPublicPathService
	shareService      shareservice.IShareService
}

type NavigateParams struct {
	Id   int    `json:"Id"`
	Path string `json:"Path"`
}

func NewFileApi(logger logger.ILogger, router *gin.Engine, config config.Config, publicPathService publicpathservice.IPublicPathService, shareService shareservice.IShareService) *FileApi {
	return &FileApi{
		router:            router,
		logger:            logger,
		publicPathService: publicPathService,
		shareService:      shareService,
		config:            config,
	}
}

func (fa *FileApi) Load() {
	fa.router.GET("api/files", fa.getFileList())
	fa.router.POST("api/files/navigate", fa.navigate())
	fa.router.GET("api/files/download/:id/*path", fa.download())

	fa.router.GET("public-api/files/download/:id", fa.downloadPublicShare())
}

func (fa *FileApi) getFileList() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		publicPaths, err := fa.publicPathService.GetBasePaths()

		if err != nil {
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "failed to load path",
			})
			return
		}

		ctx.JSON(http.StatusOK, publicPaths)
	}
}

func (fa *FileApi) navigate() gin.HandlerFunc {
	return func(ctx *gin.Context) {

		var req NavigateParams
		err := ctx.ShouldBindJSON(&req)

		if err != nil {
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "invalid request",
			})
			return
		}

		navigationName, navigation, err := fa.publicPathService.GetNavigationPaths(req.Id, req.Path)
		if err != nil {
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "failed to load path",
			})
			return
		}

		ctx.JSON(http.StatusOK, gin.H{
			"NavigationName": navigationName,
			"Entries":        navigation,
		})
	}
}

func (fa *FileApi) downloadPublicShare() gin.HandlerFunc {
	return func(ctx *gin.Context) {

		id := ctx.Param("id")

		share, err := fa.shareService.GetShareById(id)

		if err != nil {
			ctx.Redirect(http.StatusFound, utils.RedirectUri(fa.config))
		}

		fa.shareService.UpdateDownloadCount(share.Id)

		fa.handleFileOrDirectroyDownload(ctx, share.Path)
	}
}

func (fa *FileApi) download() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		validatedFilePath, success := utils.TryGetValidatedPathFromParam(ctx, fa.publicPathService)

		if !success {
			ctx.JSON(http.StatusInternalServerError, gin.H{
				"error": "failed to download",
			})
			return
		}

		fa.handleFileOrDirectroyDownload(ctx, validatedFilePath)
	}
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
	} else {
		fa.downloadFile(ctx, fileStats, path)
	}
}

func (fa *FileApi) downloadFile(ctx *gin.Context, fileStats os.FileInfo, path string) {
	ctx.Header("Content-Disposition", "attachment; filename=\""+fileStats.Name()+"\"")
	ctx.Header("Content-Type", "application/octet-stream")
	ctx.Header("Content-Length", fmt.Sprintf("%d", fileStats.Size()))
	ctx.File(path)
}

func (fa *FileApi) downloadDirectoryAsZip(ctx *gin.Context, validatedFilePath string) {
	ctx.Header("Content-Type", "application/zip")
	ctx.Header("Content-Disposition", "attachment; filename=\"download.zip\"")
	ctx.Status(http.StatusOK)

	zipWriter := zip.NewWriter(bufio.NewWriter(ctx.Writer))
	defer zipWriter.Close()

	err := filepath.Walk(validatedFilePath, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}

		relPath, err := filepath.Rel(validatedFilePath, path)
		if err != nil {
			return err
		}

		if info.IsDir() {
			relPath += "/"
		}

		header, err := zip.FileInfoHeader(info)
		if err != nil {
			return err
		}

		header.Name = relPath
		if !info.IsDir() {
			header.Method = zip.Store
		}

		writer, err := zipWriter.CreateHeader(header)
		if err != nil {
			return err
		}

		if info.IsDir() {
			return nil
		}

		file, err := os.Open(path)
		if err != nil {
			return err
		}
		defer file.Close()

		_, err = io.Copy(writer, file)
		return err
	})

	if err != nil {
		fa.logger.Warning("Aborted creating zip for path: %s", validatedFilePath)
	}
}
