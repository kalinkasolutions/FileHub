package utils

import (
	"fmt"

	"github.com/kalinkasolutions/FileHub/backend/config"
)

func RedirectUri(conf config.Config) string {
	return fmt.Sprintf("%s/404", BasePath(conf))
}

func GetShareLink(conf config.Config, shareId string) string {
	return fmt.Sprintf("%s/og/share/%s", BasePath(conf), shareId)
}

func BasePath(conf config.Config) string {
	return fmt.Sprintf("%s%s", config.CurrentProtocol(conf), conf.Domain)
}
