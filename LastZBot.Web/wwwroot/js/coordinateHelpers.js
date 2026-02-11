export function getElementSize(element) {
  const rect = element.getBoundingClientRect();
  return { width: rect.width, height: rect.height };
}
