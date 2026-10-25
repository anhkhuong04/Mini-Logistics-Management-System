import type { ShipmentDetail } from './api';
import type { components } from '../../shared/api/generated/browser-api';
import type { ShippingQuoteRequest } from './quote/api';
export type ShipmentWrite = components['schemas']['BrowserShipmentWriteRequest'];
export type ShipmentWriteResult = components['schemas']['DraftShipmentResponse'] | components['schemas']['CreateShipmentResponse'];
export function fromShipment(detail: ShipmentDetail): ShipmentWrite {
  return { senderName: detail.senderName, senderPhone: detail.senderPhone, receiverName: detail.receiverName, receiverPhone: detail.receiverPhone,
    pickupAddress: { street: detail.pickupAddress.street, ward: detail.pickupAddress.ward, province: detail.pickupAddress.province, country: detail.pickupAddress.country },
    deliveryAddress: { street: detail.deliveryAddress.street, ward: detail.deliveryAddress.ward, province: detail.deliveryAddress.province, country: detail.deliveryAddress.country },
    weightKg: detail.weightKg, lengthCm: detail.parcelLengthCm, widthCm: detail.parcelWidthCm, heightCm: detail.parcelHeightCm,
    goodsValueAmount: detail.goodsValueAmount, codAmount: detail.codAmount, currency: detail.currency, note: detail.note };
}
export function toShipmentWrite(values: ShipmentWrite): ShipmentWrite {
  // Whitelist input fields: a quote, actor, status or fee can never enter a command body.
  const { senderName, senderPhone, receiverName, receiverPhone, pickupAddress, deliveryAddress, weightKg, lengthCm, widthCm, heightCm, goodsValueAmount, codAmount, currency, note } = values;
  return { senderName, senderPhone, receiverName, receiverPhone, pickupAddress, deliveryAddress, weightKg, lengthCm, widthCm, heightCm, goodsValueAmount, codAmount, currency, note };
}
export function quoteInput(values?: Partial<ShipmentWrite>): ShippingQuoteRequest | null {
  if (!values) return null;
  const { pickupAddress, deliveryAddress, weightKg, lengthCm, widthCm, heightCm, goodsValueAmount, currency } = values;
  if (![pickupAddress, deliveryAddress].every(address => address?.street && address.ward && address.province && address.country)
    || ![weightKg, lengthCm, widthCm, heightCm].every(value => typeof value === 'string' && /^\d+(?:\.\d+)?$/.test(value) && /[1-9]/.test(value))
    || !/^\d+(?:\.\d+)?$/.test(goodsValueAmount ?? '') || currency?.length !== 3) return null;
  return { pickupAddress: pickupAddress!, deliveryAddress: deliveryAddress!, weightKg: weightKg!, lengthCm: lengthCm!, widthCm: widthCm!, heightCm: heightCm!, goodsValueAmount: goodsValueAmount!, currency };
}
