package shareapi

import (
	"net/http"
	"os"
	"path/filepath"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/api/utils"
	"github.com/kalinkasolutions/FileHub/backend/config"
	"github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
	"github.com/kalinkasolutions/FileHub/backend/services/shareservice"
)

type ShareApi struct {
	router            *gin.Engine
	logger            logger.ILogger
	publicPathService publicpathservice.IPublicPathService
	shareService      shareservice.IShareService
	config            config.Config
}

func NewShareApi(logger logger.ILogger, router *gin.Engine, config config.Config, publicPathService publicpathservice.IPublicPathService, shareService shareservice.IShareService) *ShareApi {
	return &ShareApi{
		router:            router,
		logger:            logger,
		publicPathService: publicPathService,
		shareService:      shareService,
		config:            config,
	}
}

func (ss *ShareApi) Load() {
	ss.router.GET("api/admin/shares", ss.getShares())
	ss.router.DELETE("api/admin/share", ss.deleteShare())

	ss.router.POST("api/share/create", ss.share())

	ss.router.GET(("public-api/share/validate/:id"), ss.validate())
}

func (ss *ShareApi) getShares() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		shares, err := ss.shareService.GetShares()

		if err != nil {
			ctx.JSON(http.StatusBadRequest, "Failed to get shares")
			return
		}

		if shares == nil {
			shares = []shareservice.Share{}
		}

		var result []gin.H
		for _, share := range shares {
			result = append(result, gin.H{
				"Id":               share.Id,
				"Path":             share.Path,
				"DownloadCount":    share.DownloadCount,
				"MaxDownloadCount": share.MaxDownloadCount,
				"Link":             utils.GetShareLink(ss.config, share.Id),
			})
		}

		ctx.JSON(http.StatusOK, result)
	}
}

func (ss *ShareApi) deleteShare() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var share shareservice.Share

		if err := ctx.BindJSON(&share); err != nil {
			ctx.JSON(http.StatusBadRequest, "Bad Request")
			return
		}

		deletePath, err := ss.shareService.DeleteShare(share)

		if err != nil {
			ctx.JSON(http.StatusBadRequest, "Failed to delete share")
			return
		}

		ctx.JSON(200, deletePath)
	}
}

func (ss *ShareApi) share() gin.HandlerFunc {
	return func(ctx *gin.Context) {

		var path publicpathservice.Path
		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(http.StatusBadRequest, "bad request")
			return
		}

		validatedPath, success := utils.TryGetValidatedPath(ctx, ss.publicPathService, path.Id, path.Path)

		if !success {
			return
		}

		share, err := ss.shareService.InsertShare(shareservice.Share{
			Path: validatedPath,
		})

		if err != nil {
			ctx.JSON(http.StatusBadRequest, "failed to create share")
			return
		}

		ctx.JSON(http.StatusCreated, gin.H{"Link": utils.GetShareLink(ss.config, share.Id)})
	}
}

func (ss *ShareApi) validate() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		id := ctx.Param("id")

		share, err := ss.shareService.GetShareById(id)
		if err != nil {
			ctx.Redirect(http.StatusFound, utils.RedirectUri(ss.config))
			return
		}

		info, err := os.Stat(share.Path)
		if err != nil {
			ss.logger.Error("failed to get stats for path: %s, %v", share.Path, err)
			ctx.Redirect(http.StatusFound, utils.RedirectUri(ss.config))
			return
		}

		var size int64 = 0
		err = filepath.Walk(share.Path, func(_ string, info os.FileInfo, err error) error {
			if err != nil {
				return err
			}

			if !info.IsDir() {
				size += info.Size()
			}
			return nil
		})

		if err != nil {
			ss.logger.Error("failed to walk path: %s, %v", share.Path, err)
			ctx.Redirect(http.StatusFound, utils.RedirectUri(ss.config))
			return
		}

		ctx.JSON(http.StatusOK, gin.H{"Id": share.Id, "Size": size, "Name": filepath.Base(share.Path), "IsDir": info.IsDir()})
	}
}
