import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface DonViDto {
  id: number;
  maDonVi: string;
  tenDonVi: string;
  tenVietTat?: string;
  parentId?: number;
  parentDisplayName?: string;
  diaChi?: string;
  capDonVi?: string;
  websiteNoiBo?: string;
  websiteInternet?: string;
  tongBienChe?: number;
  soDonViCapPhong: number;
  soDonViCapXa: number;
  isActive: boolean;
  children: DonViDto[];
}

export interface DonViParentCandidateDto {
  id: number;
  maDonVi: string;
  tenDonVi: string;
  tenVietTat?: string;
  level: number;
  displayName: string;
}

export interface UpsertDonViRequest {
  maDonVi: string;
  tenDonVi: string;
  tenVietTat?: string;
  parentId?: number;
  diaChi?: string;
  capDonVi?: string;
  websiteNoiBo?: string;
  websiteInternet?: string;
  tongBienChe?: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class DonViApi {
  constructor(private readonly httpClient: HttpClient) {}

  getTree(): Promise<DonViDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<DonViDto[]>>(
        `${API_BASE_URL}/don-vi/tree`,
      ),
    ).then((response) => response.data);
  }

  getParentCandidates(excludeId?: number): Promise<DonViParentCandidateDto[]> {
    const query =
      typeof excludeId === 'number' ? `?excludeId=${excludeId}` : '';
    return firstValueFrom(
      this.httpClient.get<ApiResponse<DonViParentCandidateDto[]>>(
        `${API_BASE_URL}/don-vi/parent-candidates${query}`,
      ),
    ).then((response) => response.data);
  }

  getById(id: number): Promise<DonViDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<DonViDto>>(
        `${API_BASE_URL}/don-vi/${id}`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertDonViRequest): Promise<DonViDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<DonViDto>>(
        `${API_BASE_URL}/don-vi`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(id: number, payload: UpsertDonViRequest): Promise<DonViDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<DonViDto>>(
        `${API_BASE_URL}/don-vi/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/don-vi/${id}`,
      ),
    ).then(() => undefined);
  }
}
