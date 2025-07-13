package api

import (
	"database/sql"

	"github.com/gin-gonic/gin"
	"github.com/kalinkasolutions/FileHub/backend/api/basepath"
	"github.com/kalinkasolutions/FileHub/backend/api/fileapi"
	"github.com/kalinkasolutions/FileHub/backend/api/middleware"
	"github.com/kalinkasolutions/FileHub/backend/api/shareapi"
	config "github.com/kalinkasolutions/FileHub/backend/config"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/services/basepathservice"
	"github.com/kalinkasolutions/FileHub/backend/services/publicpathservice"
	"github.com/kalinkasolutions/FileHub/backend/services/shareservice"
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

	publicPathService := publicpathservice.NewPublicPathService(a.logger, a.db)
	shareService := shareservice.NewShareservice(a.logger, a.db)

	fileapi.NewFileApi(a.logger, a.router, a.config, publicPathService, shareService).Load()
	basepath.NewBasePathApi(a.router, basepathservice.NewBasePathService(a.logger, a.db)).Load()
	shareapi.NewShareApi(a.logger, a.router, a.config, publicPathService, shareService).Load()

	a.logger.Info("Starting API on port: %s", a.config.Port)
	a.router.Run(":" + a.config.Port)
}
