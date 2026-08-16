package api

import (
	"database/sql"
	"net/http"
	"os"
	"path"
	"strings"

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

// frontendDir holds the built Angular app. It is relative to the working directory,
// so the binary has to be started from the directory that contains it.
const frontendDir = "./frontend"

// Run builds the services, registers the routes and serves until the process stops.
func Run(conf config.Config, log logger.ILogger, db *sql.DB) {
	router := newRouter(conf, log)

	publicPathService := publicpathservice.NewPublicPathService(log, db)
	basePathService := basepathservice.NewBasePathService(log, db)
	shareService := shareservice.NewShareservice(log, db)

	fileapi.Register(router, log, conf, publicPathService, shareService)
	shareapi.Register(router, log, conf, publicPathService, shareService)
	basepath.Register(router, basePathService, shareService)

	if !conf.Debug {
		serveFrontend(router)
	}

	log.Info("Starting API on port: %s", conf.Port)

	err := router.Run(":" + conf.Port)

	if err != nil {
		log.Fatal("Server stopped:\n%v", err)
	}
}

func newRouter(conf config.Config, log logger.ILogger) *gin.Engine {
	if !conf.Debug {
		gin.SetMode(gin.ReleaseMode)
	}

	router := gin.New()
	router.Use(gin.Logger())
	router.Use(gin.Recovery())

	if conf.Debug {
		router.Use(middleware.AllowAllCORS())
	}

	err := router.SetTrustedProxies(conf.TrustedProxies)

	if err != nil {
		log.Fatal("Invalid TrustedProxies in config:\n%v", err)
	}

	return router
}

// serveFrontend serves the built Angular app for anything the API did not claim:
// real files as themselves, every other path as index.html so client side routing works.
func serveFrontend(router *gin.Engine) {
	router.NoRoute(func(ctx *gin.Context) {
		requestPath := ctx.Request.URL.Path

		if isApiPath(requestPath) {
			ctx.JSON(http.StatusNotFound, gin.H{"error": "not found"})
			return
		}

		// Absolutise before cleaning so a leading ".." is dropped rather than climbing
		// out of frontendDir.
		filePath := frontendDir + path.Clean("/"+requestPath)
		info, err := os.Stat(filePath)

		if err != nil || info.IsDir() {
			ctx.File(frontendDir + "/index.html")
			return
		}

		ctx.File(filePath)
	})
}

func isApiPath(requestPath string) bool {
	return strings.HasPrefix(requestPath, "/api") || strings.HasPrefix(requestPath, "/public-api")
}
