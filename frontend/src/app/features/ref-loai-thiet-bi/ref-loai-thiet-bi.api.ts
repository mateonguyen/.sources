import { Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';
import { SUPPRESS_ERROR_TOAST } from '../../core/http/api-error.interceptor';

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
  donViId?: number | null;
  reportingLoaiId?: number | null;
  isRequired?: boolean;
  isLocal?: boolean;
  isVisible?: boolean;
  proposal?: UnitRefLoaiThietBiProposalDto;
  children: RefLoaiThietBiDto[];
}

export interface UpsertRefLoaiThietBiRequest {
  parentId: number | null;
  maLoai: string;
  tenLoai: string;
  laTongHop: boolean;
  sortOrder: number;
  isActive: boolean;
  isRequired?: boolean;
}

export interface UpsertUnitRefLoaiThietBiRequest {
  parentId: number | null;
  tenLoai: string;
  laTongHop: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface UnitRefLoaiThietBiProposalDto {
  id: number;
  parentId: number | null;
  donViId: number;
  tenDonVi: string;
  tenLoai: string;
  tenNhomCha?: string | null;
  laTongHop: boolean;
  sortOrder: number;
  soBanGhiSuDung: number;
  coLoaiCon: boolean;
}

export interface StandardizeUnitRefLoaiThietBiRequest {
  targetStandardId?: number | null;
  maLoaiMoi?: string | null;
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

  getUnitProposals(): Promise<UnitRefLoaiThietBiProposalDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<UnitRefLoaiThietBiProposalDto[]>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-proposals`,
      ),
    ).then((response) => response.data);
  }

  standardizeUnitProposal(
    id: number,
    payload: StandardizeUnitRefLoaiThietBiRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<unknown>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-proposals/${id}/standardize`,
        payload,
      ),
    ).then(() => undefined);
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

  getUnitTree(includeHidden = true): Promise<RefLoaiThietBiDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<RefLoaiThietBiDto[]>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-tree?includeHidden=${includeHidden}`,
        { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) },
      ),
    ).then((response) => response.data);
  }

  createUnitItem(payload: UpsertUnitRefLoaiThietBiRequest): Promise<RefLoaiThietBiDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<RefLoaiThietBiDto>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-items`,
        payload,
        { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) },
      ),
    ).then((response) => response.data);
  }

  updateUnitItem(id: number, payload: UpsertUnitRefLoaiThietBiRequest): Promise<RefLoaiThietBiDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<RefLoaiThietBiDto>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-items/${id}`,
        payload,
        { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) },
      ),
    ).then((response) => response.data);
  }

  deleteUnitItem(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-items/${id}`,
        { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) },
      ),
    ).then(() => undefined);
  }

  setUnitItemVisibility(id: number, isVisible: boolean): Promise<void> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<unknown>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/unit-items/${id}/visibility`,
        { isVisible },
        { context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true) },
      ),
    ).then(() => undefined);
  }
}
