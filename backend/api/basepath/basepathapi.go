package basepath

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/services/basepathservice"
	"github.com/kalinkasolutions/FileHub/backend/services/shareservice"
)

type BasePathApi struct {
	basePathService basepathservice.IBasePathService
	shareService    shareservice.IShareService
}

func Register(router *gin.Engine, basePathService basepathservice.IBasePathService, shareService shareservice.IShareService) {
	bp := &BasePathApi{
		basePathService: basePathService,
		shareService:    shareService,
	}

	router.POST("api/admin/base-path", bp.insertBasePath)
	router.GET("api/admin/base-path", bp.getBasePaths)
	router.PUT("api/admin/base-path", bp.updateBasePath)
	router.DELETE("api/admin/base-path", bp.deleteBasePath)
}

func (bp *BasePathApi) insertBasePath(ctx *gin.Context) {
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

func (bp *BasePathApi) getBasePaths(ctx *gin.Context) {
	paths, err := bp.basePathService.GetBasePaths()

	if err != nil {
		ctx.JSON(http.StatusBadRequest, err.Error())
		return
	}

	ctx.JSON(http.StatusOK, paths)
}

func (bp *BasePathApi) updateBasePath(ctx *gin.Context) {
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

	ctx.JSON(http.StatusOK, updatePath)
}

func (bp *BasePathApi) deleteBasePath(ctx *gin.Context) {
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

	// Shares hold a resolved absolute path, so they outlive their base path unless
	// they are deleted with it.
	err = bp.shareService.DeleteSharesUnderPath(deletePath.Path)

	if err != nil {
		ctx.JSON(http.StatusBadRequest, err.Error())
		return
	}

	ctx.JSON(http.StatusOK, deletePath)
}
