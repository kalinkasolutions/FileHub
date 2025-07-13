package basepath

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/services/basepathservice"
)

type BasePathApi struct {
	router          *gin.Engine
	basePathService basepathservice.IBasePathService
}

func NewBasePathApi(router *gin.Engine, basePathService basepathservice.IBasePathService) *BasePathApi {
	return &BasePathApi{
		router:          router,
		basePathService: basePathService,
	}
}

func (bp *BasePathApi) Load() {
	bp.router.POST("api/admin/base-path", bp.insertBasePath())
	bp.router.GET("api/admin/base-path", bp.getBasePaths())
	bp.router.PUT("api/admin/base-path", bp.updateBasePath())
	bp.router.DELETE("api/admin/base-path", bp.deleteBasePath())
}

func (bp *BasePathApi) insertBasePath() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var path basepathservice.Path

		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(http.StatusBadRequest, "bad request")
			return
		}

		insertedPath, err := bp.basePathService.InsertBasePath(path)

		if err != nil {
			ctx.JSON(http.StatusBadRequest, err.Error())
			return
		}

		ctx.JSON(http.StatusCreated, insertedPath)
	}
}

func (bp *BasePathApi) getBasePaths() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		paths, err := bp.basePathService.GetBasePaths()

		if err != nil {
			ctx.JSON(http.StatusBadRequest, err.Error())
			return
		}

		if paths == nil {
			paths = []basepathservice.Path{}
		}

		ctx.JSON(http.StatusOK, paths)
	}
}

func (bp *BasePathApi) updateBasePath() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var path basepathservice.Path

		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(http.StatusBadRequest, "Bad Request")
			return
		}

		updatePath, err := bp.basePathService.UpdateBasePath(path)

		if err != nil {
			ctx.JSON(http.StatusBadRequest, err.Error())
			return
		}

		ctx.JSON(200, updatePath)
	}
}

func (bp *BasePathApi) deleteBasePath() gin.HandlerFunc {
	return func(ctx *gin.Context) {
		var path basepathservice.Path

		if err := ctx.BindJSON(&path); err != nil {
			ctx.JSON(http.StatusBadRequest, "Bad Request")
			return
		}

		deletePath, err := bp.basePathService.DeleteBasePath(path)

		if err != nil {
			ctx.JSON(http.StatusBadRequest, err.Error())
			return
		}

		ctx.JSON(200, deletePath)
	}
}
