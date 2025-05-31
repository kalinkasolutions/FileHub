package fileapi

import (
	"archive/zip"
	"io"
	"net/http"
	"os"

	"github.com/gin-gonic/gin"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
)

type FileApi struct {
	router            *gin.Engine
	logger            logger.ILogger
	publicPathService publicpathservice.IPublicPathService
}

type NavigateParams struct {
	Id   int    `json:"Id"`
	Path string `json:"Path"`
}

func NewFileApi(logger logger.ILogger, router *gin.Engine, publicPathService publicpathservice.IPublicPathService) *FileApi {
	return &FileApi{
		router:            router,
		logger:            logger,
		publicPathService: publicPathService,
	}
}

func (fa *FileApi) Load() {
	fa.router.GET("api/files", fa.getFileList())
	fa.router.POST("api/files/navigate", fa.navigate())
	fa.router.GET("api/files/download-folder", fa.downloadFolderAsZip())
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

func (fa *FileApi) downloadFolderAsZip() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		ctx.Header("Content-Type", "application/zip")
		ctx.Header("Content-Disposition", "attachment; filename=\"download.zip\"")
		ctx.Status(200)

		writer := ctx.Writer

		zipWriter := zip.NewWriter(writer)
		defer zipWriter.Close()

		file, err := os.Open("/mnt/storage/movies/Young Woman and the Sea (2024)/Die.junge.Frau.und.das.Meer.2024.German.DL.1080p.DSNP.WEB.H264-Oergel.mkv")

		if err != nil {
			fa.logger.Error("Failed to open requested file: %s\n%v", file.Name(), err)
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "Failed to download file",
			})
			return
		}

		defer file.Close()

		zipFile, err := zipWriter.CreateHeader(&zip.FileHeader{
			Name: file.Name(),
		})

		if err != nil {
			fa.logger.Error("Failed to create zip entry: %s\n%v", file.Name(), err)
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "Failed to download",
			})
			return
		}

		_, err = io.Copy(zipFile, file)
		if err != nil {
			fa.logger.Error("Failed writing to client, could be due cancelation: %s\n%v", file.Name(), err)
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "Failed to download",
			})
			return
		}
	}

}
