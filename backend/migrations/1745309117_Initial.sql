CREATE TABLE IF NOT EXISTS Migrations (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    MigrationName   TEXT
);

CREATE TABLE IF NOT EXISTS Logs (
	Id							TEXT PRIMARY KEY NOT NULL,
	CreatedAt					TEXT,
	LogLevel					INTEGER,
	Message						TEXT
);