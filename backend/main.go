package main

import (
	"flag"

	api "github.com/kalinkasolutions/FileHub/backend/api"
	config "github.com/kalinkasolutions/FileHub/backend/config"
	"github.com/kalinkasolutions/FileHub/backend/datalayer"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/loggersink/consolelogsink"
)

func main() {
	logger := logger.NewLogger(consolelogsink.NewConsoleSink())
	logger.Info("starting FileHub")

	var configPath string
	flag.StringVar(&configPath, "configPath", "/app/conf.json", "path to the config")
	flag.Parse()

	config := config.LoadConfig(configPath, logger)
	db := datalayer.NewDb(logger, config)

	api := api.NewApi(config, logger, db)
	api.Load()
}
