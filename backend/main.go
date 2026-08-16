package main

import (
	"flag"

	api "github.com/kalinkasolutions/FileHub/backend/api"
	config "github.com/kalinkasolutions/FileHub/backend/config"
	"github.com/kalinkasolutions/FileHub/backend/datalayer"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	"github.com/kalinkasolutions/FileHub/backend/loggersink/consolelogsink"
	"github.com/kalinkasolutions/FileHub/backend/loggersink/dblogsink"
)

func main() {
	configPath := flag.String("configPath", "/app/conf.json", "path to the config")
	flag.Parse()

	log := logger.NewLogger(consolelogsink.NewConsoleSink())
	log.Info("starting FileHub")

	conf := config.LoadConfig(*configPath, log)
	db := datalayer.NewDb(log, conf)

	// Only now that the database exists can log lines be written to it.
	log.AddSink(dblogsink.NewDbSink(db))

	api.Run(conf, log, db)
}
