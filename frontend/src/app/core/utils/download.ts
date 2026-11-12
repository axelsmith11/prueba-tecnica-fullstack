/** Descarga un Blob como archivo con un enlace temporal. */
export function downloadFile(content: Blob, fileName: string): void {
  const url = URL.createObjectURL(content);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url));
}

/** bonos-AAAAMMDD.csv con la fecha local. La API también sugiere el nombre en Content-Disposition, pero CORS no expone ese header: el navegador nombra el archivo con el mismo patrón. */
export function csvFileName(date: Date = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `bonos-${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}.csv`;
}
