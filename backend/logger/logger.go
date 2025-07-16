package logger

import (
	"fmt"
	"os"
	"time"
)

type ILogger interface {
	Debug(message string, args ...any)
	Info(message string, args ...any)
	Warning(message string, args ...any)
	Error(message string, args ...any)
	Fatal(message string, args ...any)

	AddSink(sink ISink)
	RemoveSink(sink ISink)
}

type ISink interface {
	Name() string
	Log(message string, level int, now time.Time)
}

const (
	debug   = iota
	info    = iota
	warning = iota
	error   = iota
)

type Logger struct {
	sinks map[string]ISink
}

func NewLogger(sinks ...ISink) *Logger {
	initialSinks := make(map[string]ISink)
	for _, sink := range sinks {
		initialSinks[sink.Name()] = sink
	}
	return &Logger{
		sinks: initialSinks,
	}
}

func (l *Logger) AddSink(sink ISink) {
	l.sinks[sink.Name()] = sink
}

func (l *Logger) RemoveSink(sink ISink) {
	delete(l.sinks, sink.Name())
}

func (l *Logger) Debug(message string, args ...any) {
	l.log(message, debug, args...)
}

func (l *Logger) Info(message string, args ...any) {
	l.log(message, info, args...)
}

func (l *Logger) Warning(message string, args ...any) {
	l.log(message, warning, args...)
}

func (l *Logger) Error(message string, args ...any) {
	l.log(message, error, args...)
}

func (l *Logger) Fatal(message string, args ...any) {
	l.log(message, error, args...)
	os.Exit(1)
}

func (l *Logger) log(message string, level int, args ...any) {
	if len(args) > 0 {
		message = fmt.Sprintf(message, args...)
	}
	now := time.Now()

	for _, sink := range l.sinks {
		sink.Log(message, level, now)
	}
}

func LogLevelText(level int) string {
	switch level {
	case debug:
		return "DEBUG"
	case info:
		return "INFO"
	case warning:
		return "WARNING"
	case error:
		return "ERROR"
	default:
		return "UNKOWN"
	}
}
