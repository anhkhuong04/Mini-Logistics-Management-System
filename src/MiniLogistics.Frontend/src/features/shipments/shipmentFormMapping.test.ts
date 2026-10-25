import { expect, it } from 'vitest';
import { quoteInput, toShipmentWrite, type ShipmentWrite } from './shipmentFormMapping';
const address = { street: 'Street', ward: 'Ward', province: 'Province', country: 'Vietnam' };
const values: ShipmentWrite = { senderName: 'Sender', senderPhone: '0901234567', receiverName: 'Receiver', receiverPhone: '0901234567',
  pickupAddress: address, deliveryAddress: address, weightKg: '1', lengthCm: '10', widthCm: '10', heightCm: '10', goodsValueAmount: '0', codAmount: '9007199254740993.01', currency: 'VND' };
it('preserves exact decimal input and excludes quote actor and fee from command bodies', () => {
  const body = toShipmentWrite({ ...values, shippingFeeAmount: '1', createdByUserId: 'spoof', status: 'Delivered' } as ShipmentWrite);
  expect(body.codAmount).toBe('9007199254740993.01'); expect(body).not.toHaveProperty('shippingFeeAmount'); expect(body).not.toHaveProperty('createdByUserId'); expect(body).not.toHaveProperty('status');
  expect(quoteInput(body)).not.toHaveProperty('codAmount');
});
it('does not request a quote for incomplete or malformed inputs', () => {
  expect(quoteInput({ ...values, weightKg: '0' })).toBeNull(); expect(quoteInput({ ...values, weightKg: '1e2' })).toBeNull();
  expect(quoteInput({ ...values, deliveryAddress: { ...address, ward: '' } })).toBeNull();
  expect(quoteInput(values)?.weightKg).toBe('1');
});
