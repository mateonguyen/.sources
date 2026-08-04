import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export type TanSuat = 1 | 2 | 3;

export interface MauBaoCaoDto {
  id: number;
  maMau: string;
  tenMau: string;
  moTa?: string;
  tanSuat: TanSuat;
  danhSachModule: string[];
  isActive: boolean;
  kyBaoCaoCount: number;
}

export interface UpsertMauBaoCaoRequest {
  maMau: string;
  tenMau: string;
  moTa?: string;
  tanSuat: TanSuat;
  danhSachModule: string[];
  isActive: boolean;
}

export interface MauBaoCaoModuleCatalogItemDto {
  code: string;
  label: string;
  groupKey: string;
  groupLabel: string;
  isActive: boolean;
}

export interface MauBaoCaoModuleCatalogGroupDto {
  groupKey: string;
  groupLabel: string;
  items: MauBaoCaoModuleCatalogItemDto[];
}

@Injectable({ providedIn: 'root' })
export class MauBaoCaoApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(includeInactive = false): Promise<MauBaoCaoDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<MauBaoCaoDto[]>>(
        `${API_BASE_URL}/mau-bao-cao?includeInactive=${includeInactive}`,
      ),
    ).then((response) => response.data);
  }

  getModuleCatalog(): Promise<MauBaoCaoModuleCatalogGroupDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<MauBaoCaoModuleCatalogGroupDto[]>>(
        `${API_BASE_URL}/mau-bao-cao/module-catalog`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertMauBaoCaoRequest): Promise<MauBaoCaoDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<MauBaoCaoDto>>(
        `${API_BASE_URL}/mau-bao-cao`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(id: number, payload: UpsertMauBaoCaoRequest): Promise<MauBaoCaoDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<MauBaoCaoDto>>(
        `${API_BASE_URL}/mau-bao-cao/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/mau-bao-cao/${id}`,
      ),
    ).then(() => undefined);
  }
}
