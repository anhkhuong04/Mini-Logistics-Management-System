export const isResourceId = (id: string) =>
  /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(id) && !/^0+-0+-0+-0+-0+$/.test(id);
