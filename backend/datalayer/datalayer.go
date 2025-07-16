package datalayer

import (
	"context"
	"database/sql"
	"os"
	"path/filepath"
	"reflect"
	"regexp"
	"sort"
	"strings"

	config "github.com/kalinkasolutions/FileHub/backend/config"
	logger "github.com/kalinkasolutions/FileHub/backend/logger"
	_ "github.com/mattn/go-sqlite3"
)

func NewDb(logger logger.ILogger, config config.Config) *sql.DB {
	path := filepath.Join(config.DatabasePath, config.DatabaseName)
	logger.Info("Initializing database at %s", path)

	if config.DatabasePath != "" {
		err := os.MkdirAll(config.DatabasePath, os.ModePerm)
		if err != nil {
			logger.Error("Failed to create db direcotry at: %s\n\n%v", config.DatabasePath, err)
		}
	}

	db, err := sql.Open("sqlite3", path)

	if err != nil {
		logger.Fatal("Failed to open database at: %s\n\n%v", path, err)
	}

	migrate(logger, db)
	return db
}

func GetItems[T any](rows *sql.Rows) (out []T, err error) {
	var table []T
	for rows.Next() {
		var data T
		s := reflect.ValueOf(&data).Elem()
		numCols := s.NumField()
		columns := make([]any, numCols)

		for i := range numCols {
			field := s.Field(i)
			columns[i] = field.Addr().Interface()
		}

		if err := rows.Scan(columns...); err != nil {
			return nil, err
		}

		table = append(table, data)
	}
	return table, nil
}

func migrate(logger logger.ILogger, db *sql.DB) {
	var name string
	err := db.QueryRow("SELECT name FROM sqlite_master WHERE type='table' AND name='Migrations'").Scan(&name)

	if err == sql.ErrNoRows {
		logger.Info("No migrations applied yet")
		migrateToLatestVersion("0000000000", logger, db)
	} else if err != nil {
		logger.Fatal("Failed to migrate: %s\n\n%v", err)
	} else {
		var latestMigrationName string
		err := db.QueryRow("SELECT MigrationName FROM Migrations WHERE Id = (SELECT Max(Id) FROM Migrations)").Scan(&latestMigrationName)
		if err != nil {
			logger.Fatal("Failed to migrate: %s\n\n%v", err)
		}
		migrateToLatestVersion(latestMigrationName, logger, db)
	}
}

func migrateToLatestVersion(latestMigrationName string, logger logger.ILogger, db *sql.DB) {
	migrationDir := "./migrations/"
	files, err := os.ReadDir(migrationDir)

	if err != nil {
		logger.Fatal("Could not open migrations folder: \n%v", err)
	}

	sort.Slice(files, func(i, j int) bool {
		return files[i].Name() < files[j].Name()
	})

	for _, file := range files {
		if file.IsDir() {
			logger.Warning("Direcotry %s in migrations folder is ignored", file.Name())
			continue
		}

		if latestMigrationName >= file.Name() {
			logger.Info("Migration %s already applied", file.Name())
			continue
		}

		if !fileStartsWithUnixTimestamp(file.Name()) {
			logger.Warning("%s does not start with a unix timestamp", file.Name())
		}

		fullPath := filepath.Join(migrationDir, file.Name())

		data, err := os.ReadFile(fullPath)

		if err != nil {
			logger.Fatal("Could not read file: %s\n\n%v", fullPath, err)
		}

		statements := strings.SplitSeq(string(data), ";")

		ctx := context.Background()

		logger.Info("Applying migration: %s", file.Name())

		tx, err := db.BeginTx(ctx, nil)

		if err != nil {
			logger.Fatal("Failed to create transaction: \n%v", err)
		}

		for statement := range statements {
			statement = strings.TrimSpace(statement)
			if statement != "" {
				_, err := tx.Exec(statement)
				if err != nil {
					tx.Rollback()
					logger.Fatal("Failed to execute statement: \n%s", statement)
				}
			}
		}

		tx.Exec("INSERT INTO Migrations (MigrationName) VALUES (?)", file.Name())

		if err := tx.Commit(); err != nil {
			logger.Fatal("Failed to apply migration \n%s\n%v", file.Name(), err)
		}
	}
}

func fileStartsWithUnixTimestamp(fileName string) bool {
	re := regexp.MustCompile(`^\d{10}`)
	return re.MatchString(fileName)
}
