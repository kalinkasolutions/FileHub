package api

import (
	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/api/fileapi"
	"github.com/kalinkasolutions/FileHub/backend/api/middleware"
	config "github.com/kalinkasolutions/FileHub/backend/config"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
)

type Api struct {
	router  *gin.Engine
	config  config.Config
	logger  logger.ILogger
	fileApi fileapi.FileApi
}

func NewApi(config config.Config, logger logger.ILogger) *Api {
	return &Api{
		config: config,
		logger: logger,
	}
}

func (a *Api) Load() {
	if !a.config.Debug {
		gin.SetMode(gin.ReleaseMode)
	}

	a.router = gin.New()
	a.router.Use(gin.Logger())
	a.router.Use(gin.Recovery())

	if a.config.Debug {
		a.router.Use(middleware.AllowAllCORS())
	}

	a.router.SetTrustedProxies(a.config.TrustedProxies)

	a.router.Static("/static", "./static")

	a.logger.Info("Starting API on port: %s", a.config.Port)

	a.fileApi = *fileapi.NewFileApi(a.logger, a.router)
	a.fileApi.Load()

	a.router.Run(":" + a.config.Port)

}
