package adminapi

import (
	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/adminservice"
)

type AdminApi struct {
	logger       logger.ILogger
	router       *gin.Engine
	adminService adminservice.IAdminService
}

func NewAdminApi(logger logger.ILogger, router *gin.Engine, adminService adminservice.IAdminService) *AdminApi {
	return &AdminApi{
		router:       router,
		logger:       logger,
		adminService: adminService,
	}
}

func (aa *AdminApi) Load() {
	aa.router.POST("api/admin/base-path", aa.insertBasePath())
	aa.router.GET("api/admin/base-path", aa.getBasePaths())
	aa.router.PUT("api/admin/base-path", aa.updateBasePath())
	aa.router.DELETE("api/admin/base-path", aa.deleteBasePath())
}

func (aa *AdminApi) insertBasePath() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var path adminservice.Path

		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(400, "Bad Request")
			return
		}

		insertedPath, err := aa.adminService.InsertBasePath(path)

		if err != nil {
			ctx.JSON(400, err.Error())
			return
		}

		ctx.JSON(201, insertedPath)
	}
}

func (aa *AdminApi) getBasePaths() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		paths, err := aa.adminService.GetBasePaths()

		if err != nil {
			ctx.JSON(400, err.Error())
			return
		}

		if paths == nil {
			paths = []adminservice.Path{}
		}

		ctx.JSON(200, paths)
	}
}

func (aa *AdminApi) updateBasePath() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var path adminservice.Path

		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(400, "Bad Request")
			return
		}

		updatePath, err := aa.adminService.UpdateBasePath(path)

		if err != nil {
			ctx.JSON(400, err.Error())
			return
		}

		ctx.JSON(200, updatePath)
	}
}

func (aa *AdminApi) deleteBasePath() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var path adminservice.Path

		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(400, "Bad Request")
			return
		}

		deletePath, err := aa.adminService.DeleteBasePath(path)

		if err != nil {
			ctx.JSON(400, err.Error())
			return
		}

		ctx.JSON(200, deletePath)
	}
}
