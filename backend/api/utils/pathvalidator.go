package utils

import (
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
)

func TryGetValidatedPathFromParam(ctx *gin.Context, publicPathService publicpathservice.IPublicPathService) (string, bool) {
	paramId := ctx.Param("id")
	path := ctx.Param("path")

	id, err := strconv.Atoi(paramId)
	if err != nil {
		ctx.JSON(http.StatusBadRequest, gin.H{"error": "invalid id"})
		return "", false
	}

	return TryGetValidatedPath(ctx, publicPathService, id, path)
}

func TryGetValidatedPath(ctx *gin.Context, publicPathService publicpathservice.IPublicPathService, id int, path string) (string, bool) {

	validatedFilePath, err := publicPathService.GetValidFilePath(id, path)
	if err != nil {
		ctx.JSON(http.StatusNotFound, gin.H{"error": "file path was not found"})
		return "", false
	}

	return validatedFilePath, true
}
