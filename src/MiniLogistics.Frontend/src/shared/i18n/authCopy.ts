import { useI18n } from './I18nContext';
const vi = {
  login: 'Đăng nhập', register: 'Đăng ký Shop', email: 'Email', password: 'Mật khẩu', remember: 'Ghi nhớ đăng nhập',
  fullName: 'Họ và tên', shopName: 'Tên Shop', phoneNumber: 'Số điện thoại', addressLine: 'Địa chỉ',
  province: 'Tỉnh / Thành phố', ward: 'Phường / Xã', country: 'Quốc gia', retry: 'Thử lại',
  locked: 'Tài khoản tạm khóa. Vui lòng thử lại sau 15 phút.', credentials: 'Email hoặc mật khẩu không đúng.',
  inactive: 'Tài khoản không hoạt động.', loginFailure: 'Không thể đăng nhập. Vui lòng thử lại.',
  registerFailure: 'Không thể đăng ký. Vui lòng kiểm tra thông tin và thử lại.', referenceFailure: 'Không tải được tỉnh/phường',
  passwordRule: 'Cần chữ hoa, chữ thường, số và ký tự đặc biệt.', forbidden: 'Không có quyền truy cập',
  logout: 'Đăng xuất', logoutFailure: 'Chưa xác nhận đăng xuất. Vui lòng thử lại.',
};
const en: Record<keyof typeof vi, string> = {
  login: 'Sign in', register: 'Register a shop', email: 'Email', password: 'Password', remember: 'Remember me',
  fullName: 'Full name', shopName: 'Shop name', phoneNumber: 'Phone number', addressLine: 'Street address',
  province: 'Province / City', ward: 'Ward', country: 'Country', retry: 'Try again',
  locked: 'Account temporarily locked. Try again in 15 minutes.', credentials: 'Email or password is incorrect.',
  inactive: 'Account is inactive.', loginFailure: 'Unable to sign in. Please try again.',
  registerFailure: 'Unable to register. Check the details and try again.', referenceFailure: 'Unable to load provinces and wards',
  passwordRule: 'Include uppercase, lowercase, a digit and a special character.', forbidden: 'Access denied',
  logout: 'Sign out', logoutFailure: 'Sign-out could not be confirmed. Please try again.',
};
export function useAuthCopy() { return useI18n().locale === 'en-US' ? en : vi; }
