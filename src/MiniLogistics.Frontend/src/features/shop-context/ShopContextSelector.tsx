import { Select, Typography } from 'antd';
import { useShopContext } from './ShopContext';
import { AsyncContent } from '../../shared/ui/AsyncContent';
export function ShopContextSelector() {
  const { context, loading, error, selectShop, reload } = useShopContext();
  const selected = context?.shops.find(shop => shop.shopId === context.selectedShopId);
  return <section className="shop-context" aria-label="Shop hiện tại">
    <AsyncContent loading={loading} error={Boolean(error)} onRetry={reload}>
      {context && selected && <>
        {context.shops.length > 1 ? <><label htmlFor="selected-shop">Shop hiện tại</label>{' '}
          <Select id="selected-shop" value={context.selectedShopId} onChange={selectShop}
            options={context.shops.map(shop => ({ value: shop.shopId, label: shop.name }))} /></>
          : <Typography.Text strong>{selected.name}</Typography.Text>}
        {!selected.isActive && <p>Shop không hoạt động. Chỉ xem lịch sử theo quyền được cấp.</p>}
        <p>{selected.phoneNumber} · {selected.addressLine}, {selected.ward}, {selected.province}</p>
      </>}
    </AsyncContent>
  </section>;
}
