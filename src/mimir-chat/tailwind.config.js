/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './app/**/*.{js,ts,jsx,tsx}',
    './pages/**/*.{js,ts,jsx,tsx}',
    './components/**/*.{js,ts,jsx,tsx}',
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        nem: {
          // Core dark backgrounds (inspired by Norse midnight)
          900: '#0f1117',  // deepest background
          800: '#171923',  // sidebar / panel background
          700: '#1e2233',  // card / elevated surface
          600: '#2a2f42',  // hover / active surface
          500: '#3d4463',  // muted borders
          // Accent — teal/cyan (Mimir's wisdom)
          accent: {
            DEFAULT: '#22d3ee', // cyan-400
            hover: '#06b6d4',   // cyan-500
            muted: '#155e75',   // cyan-800
            subtle: '#0e3f52',  // cyan-900-ish
          },
          // Text
          text: {
            primary: '#f1f5f9',   // slate-100
            secondary: '#94a3b8', // slate-400
            muted: '#64748b',     // slate-500
          },
        },
      },
    },
  },
  variants: {
    extend: {
      visibility: ['group-hover'],
    },
  },
  plugins: [require('@tailwindcss/typography')],
};
