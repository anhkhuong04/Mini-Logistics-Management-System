export const queryKeys = {
  user: (userId: string) => ['user', userId] as const,
  shop: (userId: string, shopId: string, filters: Record<string, unknown> = {}) => ['user', userId, 'shop', shopId, filters] as const,
  context: (userId: string, shopId: string | null, permissionVersion: number) => ['user', userId, 'shop-context', shopId, permissionVersion] as const,
};
