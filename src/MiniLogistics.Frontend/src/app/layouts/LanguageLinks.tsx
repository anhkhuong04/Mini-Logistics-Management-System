import { useLocation } from 'react-router';
import { browserReturnPath } from '../../shared/routing/routePaths';
import { useI18n } from '../../shared/i18n/I18nContext';
export function LanguageLinks() {
  const { pathname, search, hash } = useLocation();
  const { locale } = useI18n();
  const returnUrl = browserReturnPath(`${pathname}${search}${hash}`);
  return (
    <div className="language-links" aria-label={locale === 'en-US' ? 'Language' : 'Ngôn ngữ'}>
      <a
        href={`/culture/set?${new URLSearchParams({ culture: 'vi-VN', returnUrl })}`}
        lang="vi"
        aria-current={locale === 'vi-VN' ? 'true' : undefined}
      >
        Tiếng Việt
      </a>
      <a
        href={`/culture/set?${new URLSearchParams({ culture: 'en-US', returnUrl })}`}
        lang="en"
        aria-current={locale === 'en-US' ? 'true' : undefined}
      >
        English
      </a>
    </div>
  );
}
