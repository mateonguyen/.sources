/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      fontFamily: {
        heading: ['Sora', 'Segoe UI', 'sans-serif'],
        body: ['Be Vietnam Pro', 'Segoe UI', 'sans-serif'],
      },
      boxShadow: {
        panel: '0 18px 48px rgba(17, 24, 39, 0.12)',
      },
      colors: {
        brand: {
          50: '#eef8ff',
          100: '#d9efff',
          200: '#b8dfff',
          300: '#89c8ff',
          400: '#53a7ff',
          500: '#2e83ff',
          600: '#1464f0',
          700: '#1252cd',
          800: '#1545a5',
          900: '#173d82',
        },
      },
    },
  },
  plugins: [],
};
