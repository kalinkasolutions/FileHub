package config

import (
	"bytes"
	"encoding/json"
	"os"

	logger "github.com/kalinkasolutions/FileHub/backend/logger"
)

type Config struct {
	Url            string
	DatabasePath   string
	DatabaseName   string
	Domain         string
	Port           string
	Ssl            bool
	SMTP_Username  string
	SMTP_Password  string
	SMTP_Host      string
	SMTP_Port      string
	TrustedProxies []string
	Debug          bool
}

func LoadConfig(configPath string, logger logger.ILogger) Config {

	logger.Info("Loading config from %s", configPath)

	var config Config
	configFile, err := os.ReadFile(configPath)

	if err != nil {
		logger.Fatal("Failed to open file at %s\n\n%v", configPath, err)
	}

	jsonParser := json.NewDecoder(bytes.NewReader(configFile))
	err = jsonParser.Decode(&config)

	if err != nil {
		logger.Fatal("failed to parse json: \n\n%s\n\n %v", string(configFile), err)
	}

	logger.Info("Config:\n%s", configFile)

	return config
}
