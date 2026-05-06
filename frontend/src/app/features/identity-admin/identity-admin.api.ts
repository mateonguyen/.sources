import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface UserDto {
  id: number;
  username: string;
  hoTen: string;
  email?: string;
  soDienThoai?: string;
  donViId: number;
  isActive: boolean;
  mustChangePassword: boolean;
}

export interface RoleDto {
  id: number;
  roleCode: string;
  tenRole: string;
  isSystem: boolean;
  moTa?: string | null;
  isActive?: boolean;
  permissions: string[];
}

export interface PermissionItemDto {
  id: number;
  permCode: string;
  module: string;
  action: string;
  moTa?: string;
}

export interface UserRoleMappingDto {
  userId: number;
  username: string;
  donViId: number;
  roleIds: number[];
}

export interface RolePermissionMappingDto {
  roleId: number;
  roleCode: string;
  tenRole: string;
  permissionIds: number[];
}

export interface UpdateUserRequest {
  hoTen?: string;
  email?: string | null;
  soDienThoai?: string | null;
  isActive?: boolean;
  mustChangePassword?: boolean;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  hoTen: string;
  email?: string | null;
  soDienThoai?: string | null;
  donViId: number;
}

export interface AssignRolesRequest {
  roleIds: number[];
  donViId: number;
}

export interface CreateRoleRequest {
  roleCode: string;
  tenRole: string;
  moTa?: string | null;
}

export interface UpdateRoleRequest {
  roleCode: string;
  tenRole: string;
  moTa?: string | null;
}

export interface UpdateRolePermissionsRequest {
  permissionIds: number[];
}

export interface DonViDto {
  id: number;
  maDonVi: string;
  tenDonVi: string;
  tenVietTat?: string | null;
  parentId?: number | null;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class IdentityAdminApi {
  constructor(private readonly httpClient: HttpClient) {}

  getUsers(): Promise<UserDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<UserDto[]>>(`${API_BASE_URL}/users`),
    ).then((response) => response.data);
  }

  getRoles(): Promise<RoleDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<RoleDto[]>>(`${API_BASE_URL}/roles`),
    ).then((response) => response.data);
  }

  getPermissions(): Promise<PermissionItemDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<PermissionItemDto[]>>(
        `${API_BASE_URL}/permissions`,
      ),
    ).then((response) => response.data);
  }

  getUserRoleMappings(donViId?: number): Promise<UserRoleMappingDto[]> {
    const suffix = donViId ? `?donViId=${donViId}` : '';
    return firstValueFrom(
      this.httpClient.get<ApiResponse<UserRoleMappingDto[]>>(
        `${API_BASE_URL}/phan-quyen/user-roles${suffix}`,
      ),
    ).then((response) => response.data);
  }

  getRolePermissionMappings(): Promise<RolePermissionMappingDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<RolePermissionMappingDto[]>>(
        `${API_BASE_URL}/phan-quyen/role-permissions`,
      ),
    ).then((response) => response.data);
  }

  updateUser(id: number, request: UpdateUserRequest): Promise<UserDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<UserDto>>(
        `${API_BASE_URL}/users/${id}`,
        request,
      ),
    ).then((response) => response.data);
  }

  createUser(request: CreateUserRequest): Promise<UserDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<UserDto>>(
        `${API_BASE_URL}/users`,
        request,
      ),
    ).then((response) => response.data);
  }

  createRole(request: CreateRoleRequest): Promise<RoleDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<RoleDto>>(
        `${API_BASE_URL}/roles`,
        request,
      ),
    ).then((response) => response.data);
  }

  updateRole(id: number, request: UpdateRoleRequest): Promise<RoleDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<RoleDto>>(
        `${API_BASE_URL}/roles/${id}`,
        request,
      ),
    ).then((response) => response.data);
  }

  deleteRole(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/roles/${id}`,
      ),
    ).then(() => undefined);
  }

  updateRolePermissions(
    id: number,
    request: UpdateRolePermissionsRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<unknown>>(
        `${API_BASE_URL}/roles/${id}/permissions`,
        request,
      ),
    ).then(() => undefined);
  }

  assignUserRoles(id: number, request: AssignRolesRequest): Promise<void> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<unknown>>(
        `${API_BASE_URL}/users/${id}/roles`,
        request,
      ),
    ).then(() => undefined);
  }

  getDonVis(): Promise<DonViDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<DonViDto[]>>(`${API_BASE_URL}/don-vi`),
    ).then((response) => response.data);
  }
}
