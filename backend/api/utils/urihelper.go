package utils

import (
	"fmt"

	"github.com/kalinkasolutions/FileHub/backend/config"
)

func RedirectUri(conf config.Config) string {
	return fmt.Sprintf("%s%s/404", config.CurrentProtocol(conf), conf.Domain)
}

func GetShareLink(conf config.Config, shareId string) string {
	return fmt.Sprintf("%s%s/share/%s", config.CurrentProtocol(conf), conf.Domain, shareId)
}
