import { useState, useEffect } from 'react';
import Head from 'next/head';
import { useTranslation } from 'next-i18next';
import { serverSideTranslations } from 'next-i18next/serverSideTranslations';
import { IconArrowLeft, IconSettings } from '@tabler/icons-react';
import Link from 'next/link';

const AVAILABLE_MODELS = [
  'gpt-4o',
  'gpt-4o-mini',
  'gpt-4-turbo',
  'claude-3-5-sonnet',
  'claude-3-haiku',
  'llama-3.1-70b',
];

const STORAGE_KEY = 'mimir-preferences';

interface Preferences {
  selectedModel: string;
  theme: 'dark' | 'light';
}

const DEFAULT_PREFERENCES: Preferences = {
  selectedModel: 'gpt-4o',
  theme: 'dark',
};

export default function SettingsPage() {
  const { t } = useTranslation('common');
  const [prefs, setPrefs] = useState<Preferences>(DEFAULT_PREFERENCES);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        setPrefs(JSON.parse(stored));
      }
    } catch {
      // ignore parse errors
    }
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', prefs.theme);
  }, [prefs.theme]);

  const handleSave = () => {
    if (!prefs.selectedModel) {
      setError('Please select a model.');
      return;
    }
    setError('');
    localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  return (
    <>
      <Head>
        <title>Settings - nem.Mimir</title>
      </Head>
      <div className="flex h-screen w-screen flex-col bg-[#202123] text-white overflow-auto">
        <header className="flex items-center justify-between px-6 py-4 border-b border-white/20 bg-[#343541]">
          <div className="flex items-center gap-4">
            <Link href="/" className="text-gray-400 hover:text-white transition-colors">
              <IconArrowLeft size={24} />
            </Link>
            <div className="flex items-center gap-2">
              <IconSettings size={24} />
              <h1 className="text-xl font-semibold">Settings</h1>
            </div>
          </div>
        </header>

        <main className="flex-1 max-w-2xl w-full mx-auto p-6 space-y-8">
          <section className="bg-[#343541] rounded-lg border border-white/20 p-6 space-y-4">
            <h2 className="text-lg font-semibold">Model Selection</h2>
            <div className="flex flex-col gap-2">
              <label htmlFor="model-select" className="text-sm text-gray-400">
                Default Model
              </label>
              <select
                id="model-select"
                value={prefs.selectedModel}
                onChange={(e) => setPrefs((p) => ({ ...p, selectedModel: e.target.value }))}
                className="bg-[#202123] border border-white/20 rounded p-2 text-sm text-white focus:outline-none focus:border-blue-500"
                data-testid="model-select"
              >
                <option value="">Select a model...</option>
                {AVAILABLE_MODELS.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </section>

          <section className="bg-[#343541] rounded-lg border border-white/20 p-6 space-y-4">
            <h2 className="text-lg font-semibold">Appearance</h2>
            <div className="flex items-center justify-between">
              <span className="text-sm text-gray-300">Theme</span>
              <button
                onClick={() =>
                  setPrefs((p) => ({ ...p, theme: p.theme === 'dark' ? 'light' : 'dark' }))
                }
                className="flex items-center gap-2 px-4 py-2 bg-[#202123] border border-white/20 rounded-lg text-sm hover:border-white/40 transition-colors"
                data-testid="theme-toggle"
              >
                {prefs.theme === 'dark' ? '🌙 Dark' : '☀️ Light'}
              </button>
            </div>
          </section>

          {error && (
            <p className="text-red-400 text-sm" data-testid="validation-error">
              {error}
            </p>
          )}

          <div className="flex items-center gap-4">
            <button
              onClick={handleSave}
              className="px-6 py-2 bg-blue-600 hover:bg-blue-700 rounded-lg text-sm font-medium transition-colors"
              data-testid="save-button"
            >
              Save Preferences
            </button>
            {saved && (
              <span className="text-green-400 text-sm" data-testid="saved-indicator">
                Saved!
              </span>
            )}
          </div>
        </main>
      </div>
    </>
  );
}

export const getServerSideProps = async ({ locale }: { locale: string }) => {
  return {
    props: {
      ...(await serverSideTranslations(locale ?? 'en', ['common'])),
    },
  };
};
