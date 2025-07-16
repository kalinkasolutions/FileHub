package publicpathservice

import (
	"testing"
)

func TestEscapePath(t *testing.T) {
	basePath := "/app/data"
	tests := []struct {
		name           string
		navigationPath string
		expected       string
	}{
		{"normal path", "path/subpath", "/app/data/path/subpath"},
		{"empty path", "", "/app/data"},
		{"path escape", "../../path", "/app/data/path"},
		{"path escape absolute", "/../../path", "/app/data/path"},
		{"dot path", "./path", "/app/data/path"},
		{"double slash", "path//subpath", "/app/data/path/subpath"},
		{"just escape navigation path", "path/../../../subpath", "/app/data/subpath"},
		{"absolute path in path", "path/./subpath", "/app/data/path/subpath"},
		{"absolute path in path", "/path/ /../../../other", "/app/data/other"},
	}
	for _, tt := range tests {
		result := cleanPath(basePath, tt.navigationPath)
		if result != tt.expected {
			t.Errorf("got %q, want %q", result, tt.expected)
		}
	}
}
