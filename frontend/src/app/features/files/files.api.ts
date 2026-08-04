import { Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';
import { SUPPRESS_ERROR_TOAST } from '../../core/http/api-error.interceptor';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface FileMetadataDto {
  id: number;
  entityType: string;
  entityId: number;
  fileName: string;
  mimeType: string;
  fileSize: number;
}

@Injectable({ providedIn: 'root' })
export class FilesApi {
  constructor(private readonly httpClient: HttpClient) {}

  getByEntity(entityType: string, entityId: number): Promise<FileMetadataDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<FileMetadataDto[]>>(
        `${API_BASE_URL}/files?entityType=${encodeURIComponent(entityType)}&entityId=${entityId}`,
      ),
    ).then((r) => r.data);
  }

  upload(entityType: string, entityId: number, donViId: number, file: File): Promise<FileMetadataDto> {
    const form = new FormData();
    form.append('entityType', entityType);
    form.append('entityId', String(entityId));
    form.append('donViId', String(donViId));
    form.append('file', file);
    return firstValueFrom(
      this.httpClient.post<ApiResponse<FileMetadataDto>>(`${API_BASE_URL}/files/upload`, form),
    ).then((r) => r.data);
  }

  getDownloadUrl(id: number): Promise<string> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<string>>(
        `${API_BASE_URL}/files/${id}/download-url`,
        { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) },
      ),
    ).then((r) => r.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(`${API_BASE_URL}/files/${id}`),
    ).then(() => undefined);
  }
}
