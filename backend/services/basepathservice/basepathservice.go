package basepathservice

import (
	"database/sql"
	"fmt"
	"path"
	"time"

	"github.com/kalinkasolutions/FileHub/backend/datalayer"
	"github.com/kalinkasolutions/FileHub/backend/logger"
)

type BasePathService struct {
	logger logger.ILogger
	db     *sql.DB
}

type IBasePathService interface {
	InsertBasePath(insertPath Path) (Path, error)
	GetBasePaths() ([]Path, error)
	UpdateBasePath(updatePath Path) (Path, error)
	DeleteBasePath(deletePath Path) (Path, error)
}

type Path struct {
	Id        int    `json:"id"`
	CreatedAt string `json:"createdAt"`
	Path      string `json:"path"`
}

func NewBasePathService(logger logger.ILogger, db *sql.DB) *BasePathService {
	return &BasePathService{
		logger: logger,
		db:     db,
	}
}

func (as *BasePathService) InsertBasePath(newPath Path) (Path, error) {
	cleanedPath := path.Clean(newPath.Path)
	result, err := as.db.Exec("INSERT INTO Paths (CreatedAt, Path) VALUES (?, ?)", time.Now().Format(time.RFC3339), cleanedPath)

	if err != nil {
		as.logger.Error("failed to insert path: %s\n%v", newPath, err)
		return Path{}, fmt.Errorf("failed to insert path")
	}

	lastInsertID, err := result.LastInsertId()

	if err != nil {
		as.logger.Error("failed to get inserted id from result set: %s\n%v", err)
		return Path{}, fmt.Errorf("failed to insert path")
	}

	return as.getBasePathById(int(lastInsertID))
}

func (as *BasePathService) getBasePathById(id int) (Path, error) {
	var path Path
	err := as.db.QueryRow("SELECT * FROM Paths WHERE Id = ?", id).Scan(&path.Id, &path.CreatedAt, &path.Path)

	if err != nil {
		as.logger.Error("failed to get path with id %d\n%v", id, err)
		return Path{}, fmt.Errorf("failed to get path")
	}

	return path, nil
}

func (as *BasePathService) GetBasePaths() ([]Path, error) {
	rows, err := as.db.Query("SELECT Id, CreatedAt, Path FROM Paths")

	if err != nil {
		as.logger.Error("failed to get all paths\n%v", err)
		return nil, fmt.Errorf("failed to get paths")
	}

	return datalayer.GetItems[Path](rows)
}

func (as *BasePathService) UpdateBasePath(updatePath Path) (Path, error) {
	updatePath.Path = path.Clean(updatePath.Path)
	_, err := as.db.Exec("UPDATE Paths SET Path = ? WHERE Id = ?", updatePath.Path, updatePath.Id)

	if err != nil {
		as.logger.Error("failed to update path with id: %d and pathvalue: %s", updatePath.Id, updatePath.Path)
		return Path{}, fmt.Errorf("failed to update path")
	}

	return updatePath, nil
}

func (as *BasePathService) DeleteBasePath(deletePath Path) (Path, error) {
	_, err := as.db.Exec("DELETE FROM Paths WHERE Id = ?", deletePath.Id)

	if err != nil {
		as.logger.Error("Failed to delete path with id: %d\n%v", deletePath.Id, err)
		return Path{}, fmt.Errorf("failed to delete path")
	}

	return deletePath, nil
}
