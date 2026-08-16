export interface IBasePath {
    id: number;
    createdAt: string;
    path: string;
}

export interface IBasePathModel extends IBasePath {
    edit: boolean;
    updatePath: string;
}