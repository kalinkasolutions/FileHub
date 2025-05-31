CREATE TABLE IF NOT EXISTS Migrations (
    Id              			INTEGER PRIMARY KEY AUTOINCREMENT,
    MigrationName   			TEXT
);

CREATE TABLE IF NOT EXISTS Logs (
    Id              			INTEGER PRIMARY KEY AUTOINCREMENT,
	CreatedAt					TEXT,
	LogLevel					INTEGER,
	Message						TEXT
);

CREATE TABLE IF NOT EXISTS Paths (
    Id              			INTEGER PRIMARY KEY AUTOINCREMENT,
	CreatedAt					TEXT,
	Path						TEXT
);