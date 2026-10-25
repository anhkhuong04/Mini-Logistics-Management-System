import { Select, Typography } from 'antd';
import { useShopContext } from './ShopContext';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { useI18n } from '../../shared/i18n/I18nContext';
export function ShopContextSelector({ showInactive = true }: { showInactive?: boolean }) {
  const { context, loading, error, selectShop, reload } = useShopContext();
  const { locale } = useI18n();
  const label = locale === 'en-US' ? 'Current Shop' : 'Shop hiện tại';
  const selected = context?.shops.find(shop => shop.shopId === context.selectedShopId);
  return <section className="shop-context" aria-label={label}>
    <AsyncContent loading={loading} error={Boolean(error)} onRetry={reload}>
      {context && selected && <>
        {context.shops.length > 1 ? <><label htmlFor="selected-shop">{label}</label>{' '}
          <Select id="selected-shop" value={context.selectedShopId} onChange={selectShop}
            options={context.shops.map(shop => ({ value: shop.shopId, label: shop.name }))} /></>
          : <Typography.Text strong>{selected.name}</Typography.Text>}
        {showInactive && !selected.isActive && <p>{locale === 'en-US' ? 'Shop is inactive. History remains available according to your permissions.' : 'Shop không hoạt động. Chỉ xem lịch sử theo quyền được cấp.'}</p>}
        <p>{selected.phoneNumber} · {selected.addressLine}, {selected.ward}, {selected.province}</p>
      </>}
    </AsyncContent>
  </section>;
}
