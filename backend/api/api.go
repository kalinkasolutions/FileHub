package api

import (
	"database/sql"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/api/adminapi"
	"github.com/kalinkasolutions/FileHub/backend/api/fileapi"
	"github.com/kalinkasolutions/FileHub/backend/api/middleware"
	config "github.com/kalinkasolutions/FileHub/backend/config"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/adminservice"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
)

type Api struct {
	router *gin.Engine
	config config.Config
	logger logger.ILogger
	db     *sql.DB
}

func NewApi(config config.Config, logger logger.ILogger, db *sql.DB) *Api {
	return &Api{
		config: config,
		logger: logger,
		db:     db,
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

	publicPathService := publicpathservice.NewPublicPathService(a.logger, a.db)
	fileApi := fileapi.NewFileApi(a.logger, a.router, publicPathService)
	fileApi.Load()

	adminService := adminservice.NewAdminService(a.logger, a.db)
	adminApi := adminapi.NewAdminApi(a.logger, a.router, adminService)
	adminApi.Load()

	a.router.Run(":" + a.config.Port)
}
