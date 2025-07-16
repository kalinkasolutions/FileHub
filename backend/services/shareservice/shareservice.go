package shareservice

import (
	"database/sql"
	"fmt"
	"time"

	"github.com/google/uuid"
	"github.com/kalinkasolutions/FileHub/backend/datalayer"
	"github.com/kalinkasolutions/FileHub/backend/logger"
)

type ShareService struct {
	logger logger.ILogger
	db     *sql.DB
}

type IShareService interface {
	InsertShare(insertPath Share) (Share, error)
	GetShares() ([]Share, error)
	GetShareById(id string) (Share, error)
	UpdateDownloadCount(id string) error
	DeleteShare(deletePath Share) (Share, error)
}

type Share struct {
	Id               string `json:"Id"`
	CreatedAt        string `json:"CreatedAt"`
	Path             string `json:"Path"`
	DownloadCount    int    `json:"DownloadCount"`
	MaxDownloadCount int    `json:"MaxDownloadCount"`
}

func NewShareservice(logger logger.ILogger, db *sql.DB) *ShareService {
	return &ShareService{
		logger: logger,
		db:     db,
	}
}

func (as *ShareService) InsertShare(newShare Share) (Share, error) {
	id := uuid.New().String()

	_, err := as.db.Exec("INSERT INTO Shares (Id, CreatedAt, Path, downloadCount, maxDownloadCount) VALUES (?, ?, ?, ?, ?)", id, time.Now().Format(time.RFC3339), newShare.Path, 0, 0)

	if err != nil {
		as.logger.Error("failed to insert share: %s\n%v", newShare, err)
		return Share{}, fmt.Errorf("failed to insert share")
	}

	return as.GetShareById(id)
}

func (as *ShareService) GetShareById(id string) (Share, error) {
	var share Share
	err := as.db.QueryRow("SELECT Id, CreatedAt, Path, DownloadCount, MaxDownloadCount FROM Shares WHERE Id = ?", id).Scan(&share.Id, &share.CreatedAt, &share.Path, &share.DownloadCount, &share.MaxDownloadCount)

	if err != nil {
		as.logger.Error("failed to get share with id %s\n%v", id, err)
		return Share{}, fmt.Errorf("failed to get share")
	}

	return share, nil
}

func (as *ShareService) GetShares() ([]Share, error) {
	rows, err := as.db.Query("SELECT Id, CreatedAt, Path, DownloadCount, MaxDownloadCount FROM Shares")

	if err != nil {
		as.logger.Error("failed to get all share\n%v", err)
		return nil, fmt.Errorf("failed to get share")
	}

	return datalayer.GetItems[Share](rows)
}

func (as *ShareService) UpdateDownloadCount(id string) error {
	_, err := as.db.Exec("UPDATE Shares SET DownloadCount = DownloadCount + 1 WHERE Id = ?", id)

	if err != nil {
		as.logger.Error("failed to increment downloadcount share with id: %s", id)
		return fmt.Errorf("failed to update share")
	}

	return nil
}

func (as *ShareService) DeleteShare(deleteShare Share) (Share, error) {
	_, err := as.db.Exec("DELETE FROM Shares WHERE Id = ?", deleteShare.Id)

	if err != nil {
		as.logger.Error("Failed to delete share with id: %d\n%v", deleteShare.Id, err)
		return Share{}, fmt.Errorf("failed to delete share")
	}

	return deleteShare, nil
}
