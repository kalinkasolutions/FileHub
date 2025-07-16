package dblogsink

import (
	"database/sql"
	"log"
	"time"
)

type DbSink struct {
	db *sql.DB
}

func NewDbSink(db *sql.DB) *DbSink {
	return &DbSink{
		db: db,
	}
}

func (d DbSink) Name() string {
	return "dblogger"
}

func (d *DbSink) Log(message string, level int, now time.Time) {
	_, err := d.db.Exec(`
	INSERT INTO Logs 
		(CreatedAt, LogLevel, Message) 
		VALUES (?, ?, ?)`, now.UTC(), level, message)
	if err != nil {
		log.Printf("Error: Failed to insert log: %v", err)
	}
}
