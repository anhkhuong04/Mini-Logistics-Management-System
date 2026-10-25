import type { ThemeConfig } from 'antd';
export const antdTheme: ThemeConfig = {
  token: {
    colorPrimary: '#0077c8', colorPrimaryHover: '#005b9f',
    colorText: '#17384e', colorTextHeading: '#102f44', colorTextSecondary: '#546b7b',
    colorBorder: '#7b8f9a', colorBorderSecondary: '#dbe8f2',
    colorBgContainer: '#ffffff', colorBgLayout: '#f7faff',
    colorSuccess: '#087a55', colorWarning: '#a15c07', colorError: '#b42318',
    borderRadius: 12, borderRadiusSM: 8, borderRadiusLG: 16,
    fontFamily: "'Be Vietnam Pro', 'Segoe UI', Arial, sans-serif",
    fontSize: 14, controlHeight: 40, padding: 16, paddingLG: 24,
  },
  components: {
    Layout: { headerBg: '#ffffff', siderBg: '#ffffff', bodyBg: '#f7faff' },
    Menu: { itemSelectedBg: '#eaf5ff', itemSelectedColor: '#005b9f', itemHeight: 44 },
  },
};
