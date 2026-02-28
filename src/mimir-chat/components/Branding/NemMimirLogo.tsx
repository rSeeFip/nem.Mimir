import { FC } from 'react';

interface Props {
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const sizeMap = {
  sm: 'text-lg',
  md: 'text-2xl',
  lg: 'text-4xl',
};

export const NemMimirLogo: FC<Props> = ({ size = 'md', className = '' }) => {
  return (
    <span
      className={`font-bold select-none ${sizeMap[size]} ${className}`}
      aria-label="nem.Mimir"
    >
      <span className="text-[#22d3ee]">nem</span>
      <span className="text-[#64748b]">.</span>
      <span className="text-white">Mimir</span>
    </span>
  );
};
