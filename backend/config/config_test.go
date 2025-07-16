package config

import (
	"os"
	"testing"

	"github.com/go-playground/assert"
	mocks "github.com/kalinkasolutions/FileHub/backend/mocks"
)

func TestConfig(t *testing.T) {
	path := "/tmp/config.json"
	err := os.WriteFile(path, []byte(`{
    "DatabasePath": "DatabasePath",
    "DatabaseName": "DatabaseName",
    "Ssl": false,
    "Domain": "Domain",
    "Port": "Port",
    "SMTP_Username": "SMTP_Username",
    "SMTP_Password": "SMTP_Password",
    "SMTP_Host": "SMTP_Host",
    "SMTP_Port": "SMTP_Port",
	"TrustedProxies": ["TrustedProxy_1", "TrustedProxy_2"],
    "Debug": false
}`), 0644)

	assert.Equal(t, nil, err)

	config := LoadConfig(path, mocks.NewLoggerMock())

	assert.Equal(t, "DatabasePath", config.DatabasePath)
	assert.Equal(t, "DatabaseName", config.DatabaseName)
	assert.Equal(t, false, config.Ssl)
	assert.Equal(t, "Domain", config.Domain)
	assert.Equal(t, "Port", config.Port)
	assert.Equal(t, "SMTP_Username", config.SMTP_Username)
	assert.Equal(t, "SMTP_Password", config.SMTP_Password)
	assert.Equal(t, "SMTP_Host", config.SMTP_Host)
	assert.Equal(t, "SMTP_Port", config.SMTP_Port)
	assert.Equal(t, "TrustedProxy_1", config.TrustedProxies[0])
	assert.Equal(t, "TrustedProxy_2", config.TrustedProxies[1])
	assert.Equal(t, false, config.Debug)

}
