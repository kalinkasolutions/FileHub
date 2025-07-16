export class FileSize {
    public static FileSize(size: number): string {
        const gigabytes = Number((size / 1000 / 1000 / 1000).toFixed(2));
        if (gigabytes >= 1) {
            75
            return `${gigabytes} Gb`;
        }

        const megabytes = Number((size / 1000 / 1000).toFixed(2));
        if (megabytes >= 1) {
            return `${megabytes} Mb`;
        }

        const kilobytes = Number((size / 1000).toFixed(2));
        if (kilobytes >= 1) {
            return `${kilobytes} Kb`;
        }

        if (size >= 1) {
            return `${size} bytes`
        }

        return "";
    }
}