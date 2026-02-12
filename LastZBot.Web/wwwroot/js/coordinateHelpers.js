export function getElementSize(element) {
  const rect = element.getBoundingClientRect();
  return { width: rect.width, height: rect.height };
}

export function capturePointerUp(imgElement, dotNetRef) {
  const handler = async (e) => {
    document.removeEventListener('mouseup', handler);
    const rect = imgElement.getBoundingClientRect();
    const offsetX = e.clientX - rect.left;
    const offsetY = e.clientY - rect.top;
    await dotNetRef.invokeMethodAsync('OnPointerUp', offsetX, offsetY);
  };
  document.addEventListener('mouseup', handler);
}
