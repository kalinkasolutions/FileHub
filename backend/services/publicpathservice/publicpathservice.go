package publicpathservice

import (
	"database/sql"
	"fmt"
	"os"
	"path"
	"strings"

	"github.com/google/uuid"
	"github.com/kalinkasolutions/FileHub/backend/datalayer"
	"github.com/kalinkasolutions/FileHub/backend/logger"
)

type PubliPathService struct {
	logger logger.ILogger
	db     *sql.DB
}

type IPublicPathService interface {
	GetBasePaths() ([]PublicPath, error)
	GetNavigationPaths(basePathId int, navigationPath string) (string, []PublicPath, error)
	GetValidFilePath(basePathId int, navigationPath string) (string, error)
}

type PublicPath struct {
	Id          int
	Name        string
	IsDir       bool
	Size        int64
	NextSegment string
	IsBasePath  bool
	ItemId      string
}

type Path struct {
	Id       int
	Path     string
	BasePath string
}

func NewPublicPathService(logger logger.ILogger, db *sql.DB) *PubliPathService {
	return &PubliPathService{
		logger: logger,
		db:     db,
	}
}

func (pps *PubliPathService) GetBasePaths() ([]PublicPath, error) {
	rows, err := pps.db.Query("SELECT Id, Path, Path AS BasePath FROM Paths")

	if err != nil {
		pps.logger.Error("failed to get public paths\n%v", err)
		return nil, fmt.Errorf("failed to get public paths")
	}

	storedPaths, err := datalayer.GetItems[Path](rows)

	if err != nil {
		pps.logger.Error("failed to get public paths\n%v", err)
		return nil, fmt.Errorf("failed to get public paths")
	}

	return pps.getDirectoryInfos(storedPaths), nil
}

func (pps *PubliPathService) GetNavigationPaths(basePathId int, navigationPath string) (string, []PublicPath, error) {
	basePath, err := pps.getBasePath(basePathId)
	if err != nil {
		return "", nil, fmt.Errorf("failed to navigate")
	}

	finalPath := cleanPath(basePath.Path, navigationPath)

	entries, err := os.ReadDir(finalPath)
	if err != nil {
		pps.logger.Error("failed to get public path with id: %d\n%v", basePathId, err)
		return "", nil, fmt.Errorf("failed to navigate")
	}

	pathEntries := make([]Path, 0, len(entries))

	for _, entry := range entries {
		pathEntries = append(pathEntries, Path{
			Id:       basePathId,
			Path:     path.Join(finalPath, entry.Name()),
			BasePath: basePath.Path,
		})
	}

	return path.Base(finalPath), pps.getDirectoryInfos(pathEntries), nil
}

func (pps *PubliPathService) GetValidFilePath(basePathId int, navigationPath string) (string, error) {
	basePath, err := pps.getBasePath(basePathId)
	if err != nil {
		return "", fmt.Errorf("path not valid")
	}

	return cleanPath(basePath.Path, navigationPath), nil
}

func (pps *PubliPathService) getBasePath(basePathId int) (Path, error) {
	var basePath Path

	err := pps.db.QueryRow("SELECT Id, Path FROM Paths WHERE Id = ?", basePathId).Scan(&basePath.Id, &basePath.Path)

	if err != nil {
		pps.logger.Error("failed to get public path with id: %d\n%v", basePathId, err)
		return basePath, err
	}
	return basePath, nil
}

func (pps *PubliPathService) getDirectoryInfos(directory []Path) []PublicPath {
	publicPaths := make([]PublicPath, 0, len(directory))

	for _, entry := range directory {
		fileInfo := pps.getFileInfo(entry.Path)
		if fileInfo == nil {
			continue
		}

		publicPaths = append(publicPaths, PublicPath{
			Id:          entry.Id,
			Name:        path.Base(entry.Path),
			IsDir:       fileInfo.IsDir(),
			Size:        fileInfo.Size(),
			NextSegment: strings.TrimPrefix(entry.Path, entry.BasePath),
			IsBasePath:  entry.Path == entry.BasePath,
			ItemId:      uuid.New().String(),
		})
	}
	return publicPaths
}

func cleanPath(basePath string, navigation string) string {

	if navigation == "" {
		return basePath
	}

	if !strings.HasPrefix(navigation, "/") {
		navigation = "/" + navigation
	}

	navigation = path.Clean(navigation)

	return path.Join(basePath, navigation)
}

func (pps *PubliPathService) getFileInfo(infoPath string) os.FileInfo {
	info, err := os.Stat(infoPath)
	if err != nil {
		pps.logger.Error("Failed to read fileinfo for: %s\n%v", infoPath, err)
		return nil
	}

	return info
}
