package consolelogsink

import (
	"fmt"
	"time"

	logger "github.com/kalinkasolutions/FileHub/backend/logger"
)

type ConsoleSink struct {
}

func NewConsoleSink() *ConsoleSink {
	return &ConsoleSink{}
}

func (l *ConsoleSink) Name() string {
	return "consolelogger"
}

func (l *ConsoleSink) Log(message string, level int, now time.Time) {
	fmt.Println("\033[" + colorStartTag(level) + now.Format(time.ANSIC) + "\t" + logger.LogLevelText(level) + "\t\t" + message + "\033[0m")
}

func colorStartTag(level int) string {
	switch level {
	case 0:
		return "34m"
	case 1:
		return "32m"
	case 2:
		return "33m"
	case 3:
		return "31m"
	default:
		return "37m"
	}
}
