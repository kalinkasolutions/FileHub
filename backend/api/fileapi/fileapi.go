package fileapi

import (
	"archive/zip"
	"io"
	"net/http"
	"os"
	"path/filepath"

	"github.com/gin-gonic/gin"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
)

type FileApi struct {
	router *gin.Engine
	logger logger.ILogger
}

type FileInfo struct {
	Name  string `json:"name"`
	IsDir bool   `json:"isDir"`
	Size  int64  `json:"size"`
}

func NewFileApi(logger logger.ILogger, router *gin.Engine) *FileApi {
	return &FileApi{
		router: router,
		logger: logger,
	}
}

func (fa *FileApi) Load() {
	fa.router.GET("/admin/files", fa.getFileList())
	fa.router.GET("/admin/files/download-folder", fa.downloadFolderAsZip())
}

func (fa *FileApi) getFileList() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		directoryName := ctx.DefaultQuery("directoryName", "")
		newPath := filepath.Join("/mnt/storage", directoryName)
		files, err := os.ReadDir(newPath)

		if err != nil {
			fa.logger.Error("Failed to load directories: %s\n%v", newPath, err)
			ctx.JSON(http.StatusBadRequest, gin.H{
				"error": "Failed to load directories",
			})
			return
		}

		var fileDetails []FileInfo
		for _, file := range files {
			if info, err := file.Info(); err != nil {
				fa.logger.Error("Failed to read fileinfo: %s %s\n%v", newPath, file.Name(), err)
				ctx.JSON(http.StatusBadRequest, gin.H{
					"error": "Failed to load directories",
				})
				return
			} else {
				fileDetails = append(fileDetails, FileInfo{
					Name:  file.Name(),
					IsDir: file.IsDir(),
					Size:  info.Size(),
				})
			}
		}

		ctx.JSON(http.StatusOK, fileDetails)

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
