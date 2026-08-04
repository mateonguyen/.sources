import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface RefLoaiThietBiDto {
  id: number;
  parentId: number | null;
  maLoai: string;
  tenLoai: string;
  cap: number;
  laTongHop: boolean;
  sortOrder: number;
  isActive: boolean;
  children: RefLoaiThietBiDto[];
}

export interface UpsertRefLoaiThietBiRequest {
  parentId: number | null;
  maLoai: string;
  tenLoai: string;
  laTongHop: boolean;
  sortOrder: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class RefLoaiThietBiApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAdminTree(): Promise<RefLoaiThietBiDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<RefLoaiThietBiDto[]>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/admin-tree`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertRefLoaiThietBiRequest): Promise<RefLoaiThietBiDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<RefLoaiThietBiDto>>(
        `${API_BASE_URL}/ref-loai-thiet-bi`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(
    id: number,
    payload: UpsertRefLoaiThietBiRequest,
  ): Promise<RefLoaiThietBiDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<RefLoaiThietBiDto>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/${id}`,
      ),
    ).then(() => undefined);
  }
}
