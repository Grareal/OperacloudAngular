import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  AccessAdministration, AccompanyingGuestChangePreview, AccompanyingGuestChangeResult,
  ApiEnvironmentInfo, CommunicationDocumentInfo, GuestEmailDeliveryInfo, GuestEmailSettingInfo,
  HotelDocumentSetting, OcrParseResult, OfficialCardInput, OperaAttachmentResponse, OperaPromotionCodeInfo,
  PdfFieldCatalogItem, PdfTemplateDetail, PdfTemplateFieldInfo, PdfTemplateSummary, PromotionCatalogInfo,
  Reservation, ReservationDocumentPackage, StoredSignatureInfo,
} from './models';

const B = '/api';

/** Cliente de transición. La sesión se mantiene en una cookie HttpOnly. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  getEnvironment() { return firstValueFrom(this.http.get<ApiEnvironmentInfo>(`${B}/environment`)); }

  getReservation(c: string) { return firstValueFrom(this.http.get<Reservation[]>(`${B}/reservations/${encodeURIComponent(c)}`)); }
  searchReservations(term: string) { return firstValueFrom(this.http.get<Reservation[]>(`${B}/reservations/search?term=${encodeURIComponent(term)}`)); }
  getArrivals(s: string, e: string, limit: number) { return firstValueFrom(this.http.get<Reservation[]>(`${B}/reservations/arrivals?start=${s}&end=${e}&limit=${limit}`)); }
  getDepartures(s: string, e: string, limit: number) { return firstValueFrom(this.http.get<Reservation[]>(`${B}/reservations/departures?start=${s}&end=${e}&limit=${limit}`)); }

  previewOfficialCard(c: string, input: OfficialCardInput) {
    return firstValueFrom(this.http.post(`${B}/reservations/${encodeURIComponent(c)}/official-registration-card/preview`, input, { responseType: 'blob' }));
  }
  uploadOfficialCard(c: string, input: OfficialCardInput) {
    return firstValueFrom(this.http.post<OperaAttachmentResponse>(`${B}/reservations/${encodeURIComponent(c)}/official-registration-card/upload`, input));
  }
  getOfficialCard(c: string) {
    return firstValueFrom(this.http.get(`${B}/reservations/${encodeURIComponent(c)}/official-registration-card`, { responseType: 'blob' }));
  }

  getRecentLocalDocuments(limit = 100) { return firstValueFrom(this.http.get<any[]>(`${B}/local-documents/recent?limit=${limit}`, { headers: this.reason('Consulta de historial operativo') })); }
  getReservationPackage(c: string, reason: string) { return firstValueFrom(this.http.get<ReservationDocumentPackage>(`${B}/local-documents/reservation/${encodeURIComponent(c)}`, { headers: this.reason(reason) })); }
  downloadLocalDocument(id: string, reason: string) { return firstValueFrom(this.http.get(`${B}/local-documents/${id}/pdf`, { responseType: 'blob', headers: this.reason(reason) })); }
  downloadEmailItem(id: string, reason: string) { return firstValueFrom(this.http.get(`${B}/local-documents/email-items/${id}/file`, { responseType: 'blob', headers: this.reason(reason) })); }
  sealReservationFile(id: string, reason: string) { return firstValueFrom(this.http.post(`${B}/local-documents/reservation-files/${id}/seal`, { reason })); }

  searchStoredSignatures(term: string) { return firstValueFrom(this.http.get<StoredSignatureInfo[]>(`${B}/stored-signatures?term=${encodeURIComponent(term)}`)); }
  getSignatureImage(id: string, reason = 'Consulta de firma vinculada al expediente') { return firstValueFrom(this.http.get(`${B}/stored-signatures/${id}/image`, { responseType: 'blob', headers: this.reason(reason) })); }

  getPdfTemplates() { return firstValueFrom(this.http.get<PdfTemplateSummary[]>(`${B}/pdf-templates`)); }
  getPdfFieldCatalog() { return firstValueFrom(this.http.get<PdfFieldCatalogItem[]>(`${B}/pdf-templates/catalog`)); }
  getPdfTemplate(id: string) { return firstValueFrom(this.http.get<PdfTemplateDetail>(`${B}/pdf-templates/${id}`)); }
  getPdfTemplateFile(id: string) { return firstValueFrom(this.http.get(`${B}/pdf-templates/${id}/pdf`, { responseType: 'blob' })); }
  uploadPdfTemplate(hotelId: string, name: string, templateType: string, file: File) {
    const f = new FormData();
    f.append('hotelId', hotelId); f.append('name', name); f.append('templateType', templateType); f.append('file', file, file.name);
    return firstValueFrom(this.http.post<{ id: string }>(`${B}/pdf-templates`, f));
  }
  savePdfTemplateFields(id: string, fields: PdfTemplateFieldInfo[]) { return firstValueFrom(this.http.put(`${B}/pdf-templates/${id}/fields`, fields)); }
  renderPdfTemplate(id: string, confirmation: string) { return firstValueFrom(this.http.get(`${B}/pdf-templates/${id}/render/${encodeURIComponent(confirmation)}`, { responseType: 'blob' })); }
  publishPdfTemplate(id: string, isDefault: boolean, roomTypePrefix?: string, language?: string) { return firstValueFrom(this.http.post(`${B}/pdf-templates/${id}/publish`, { isDefault, roomTypePrefix, language })); }
  unpublishPdfTemplate(id: string) { return firstValueFrom(this.http.post(`${B}/pdf-templates/${id}/unpublish`, {})); }
  getHotelDocumentSetting(hotelId: string) { return firstValueFrom(this.http.get<HotelDocumentSetting>(`${B}/pdf-templates/settings/${encodeURIComponent(hotelId)}`)); }
  saveHotelDocumentSetting(hotelId: string, sourceMode: string, preferredPdfTemplateId?: string) { return firstValueFrom(this.http.put(`${B}/pdf-templates/settings/${encodeURIComponent(hotelId)}`, { sourceMode, preferredPdfTemplateId })); }

  getCommunicationDocuments(hotelId: string) { return firstValueFrom(this.http.get<CommunicationDocumentInfo[]>(`${B}/communication-documents?hotelId=${encodeURIComponent(hotelId)}`)); }
  uploadCommunicationDocument(edit: any, file: File) {
    const f = new FormData();
    for (const k of ['HotelId','Type','Name','Language','IsRequired','RequiresMarketingConsent','SortOrder','EffectiveFromUtc','EffectiveToUtc'])
      if (edit[k] != null) f.append(k, String(edit[k]));
    f.append('File', file, file.name);
    return firstValueFrom(this.http.post(`${B}/communication-documents/upload`, f));
  }
  addRemoteCommunicationDocument(edit: any, sourceUrl: string, fileName: string) {
    return firstValueFrom(this.http.post(`${B}/communication-documents/remote`, { ...edit, sourceUrl, fileName, contentType: 'application/pdf' }));
  }
  saveCommunicationDocumentPublication(id: string, edit: any, published: boolean) {
    return firstValueFrom(this.http.put(`${B}/communication-documents/${id}/publication`, { ...edit, isPublished: published }));
  }
  getGuestEmailDeliveries(hotelId: string) { return firstValueFrom(this.http.get<GuestEmailDeliveryInfo[]>(`${B}/communication-documents/deliveries?hotelId=${encodeURIComponent(hotelId)}`)); }
  retryGuestEmailDelivery(id: string) { return firstValueFrom(this.http.post(`${B}/communication-documents/deliveries/${id}/retry`, {})); }

  getPromotionCatalog(hotelId: string, language: string, search: string, status: string) {
    return firstValueFrom(this.http.get<PromotionCatalogInfo[]>(`${B}/promotion-catalog?hotelId=${encodeURIComponent(hotelId)}&language=${encodeURIComponent(language)}&status=${encodeURIComponent(status)}&search=${encodeURIComponent(search ?? '')}`));
  }
  getOperaPromotionCodes(hotelId: string) { return firstValueFrom(this.http.get<OperaPromotionCodeInfo[]>(`${B}/promotion-catalog/opera?hotelId=${encodeURIComponent(hotelId)}`)); }
  savePromotionCatalogEntry(input: any) { return firstValueFrom(this.http.post(`${B}/promotion-catalog`, input)); }
  updatePromotionCatalogEntry(id: string, input: any) { return firstValueFrom(this.http.put(`${B}/promotion-catalog/${id}`, input)); }
  importPromotionCatalog(hotelId: string, language: string, file: File) {
    const f = new FormData();
    f.append('HotelId', hotelId); f.append('Language', language); f.append('EntityName', 'VIDA_PROMOTIONSTSW'); f.append('File', file, file.name);
    return firstValueFrom(this.http.post<{ message: string }>(`${B}/promotion-catalog/import`, f));
  }

  getEmailSetting(hotelId: string) { return firstValueFrom(this.http.get<GuestEmailSettingInfo>(`${B}/email-settings/${encodeURIComponent(hotelId)}`)); }
  saveEmailSetting(hotelId: string, input: GuestEmailSettingInfo) { return firstValueFrom(this.http.put(`${B}/email-settings/${encodeURIComponent(hotelId)}`, input)); }
  testEmailSetting(hotelId: string, recipient: string) { return firstValueFrom(this.http.post<{ message: string }>(`${B}/email-settings/${encodeURIComponent(hotelId)}/test`, { recipient })); }

  lookupAccompanyingAdult(c: string, givenName: string, surname: string) {
    return firstValueFrom(this.http.get<any>(`${B}/reservations/${encodeURIComponent(c)}/accompanying-guests/lookup-adult?givenName=${encodeURIComponent(givenName)}&surname=${encodeURIComponent(surname)}`));
  }
  createAndAddAccompanyingAdult(c: string, givenName: string, surname: string, expectedLastModifyDateTime: string, confirmationText: string) {
    return firstValueFrom(this.http.post<AccompanyingGuestChangeResult>(`${B}/reservations/${encodeURIComponent(c)}/accompanying-guests/create-and-add-adult`, { givenName, surname, expectedLastModifyDateTime, confirmationText }));
  }
  previewAddAccompanyingAdult(c: string, profileId: string) {    return firstValueFrom(this.http.get<AccompanyingGuestChangePreview>(`${B}/reservations/${encodeURIComponent(c)}/accompanying-guests/preview-add-adult?profileId=${encodeURIComponent(profileId)}`));
  }
  addAccompanyingAdult(c: string, profileId: string, expectedLastModifyDateTime: string, confirmationText: string) {
    return firstValueFrom(this.http.post<AccompanyingGuestChangeResult>(`${B}/reservations/${encodeURIComponent(c)}/accompanying-guests/add-adult`, { profileId, expectedLastModifyDateTime, confirmationText }));
  }

  getAccessAdministration() { return firstValueFrom(this.http.get<AccessAdministration>(`${B}/admin/access`)); }
  saveUserGroup(id: number | null, input: any) {
    return firstValueFrom(id ? this.http.put(`${B}/admin/access/groups/${id}`, input) : this.http.post(`${B}/admin/access/groups`, input));
  }
  saveAppUser(id: number | null, input: any) {
    return firstValueFrom(id ? this.http.put(`${B}/admin/access/users/${id}`, input) : this.http.post(`${B}/admin/access/users`, input));
  }

  parseIdentity(front: File, back: File | null, documentType: string) {
    const f = new FormData();
    f.append('front', front, front.name);
    if (back) f.append('back', back, back.name);
    f.append('documentType', documentType);
    return firstValueFrom(this.http.post<OcrParseResult>(`${B}/ocr/parse`, f));
  }
  createIdentityPdf(confirmation: string, hotelId: string, room: string, front: File, back: File | null, documentType: string, reviewedFields: unknown) {
    const f = new FormData();
    f.append('confirmationNumber', confirmation); f.append('hotelId', hotelId);
    if (room) f.append('roomNumber', room);
    f.append('front', front, front.name);
    if (back) f.append('back', back, back.name);
    f.append('documentType', documentType);
    f.append('reviewedFieldsJson', JSON.stringify(reviewedFields));
    f.append('reviewConfirmed', 'true');
    f.append('retentionAccepted', 'true');
    return firstValueFrom(this.http.post(`${B}/ocr/identity-pdf`, f, { responseType: 'blob', observe: 'response' }));
  }

  searchAudit(confirmationNumber = '', actor = '') {
    return firstValueFrom(this.http.get<any[]>(`${B}/audit?confirmationNumber=${encodeURIComponent(confirmationNumber)}&actor=${encodeURIComponent(actor)}`));
  }
  verifyAuditIntegrity() { return firstValueFrom(this.http.get<{ valid: boolean; brokenAt?: string; count: number }>(`${B}/audit/integrity`)); }

  downloadBlob(b: Blob, fileName: string) {
    const a = document.createElement('a');
    a.href = URL.createObjectURL(b); a.download = fileName; a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 5000);
  }

  private reason(value: string) { return { 'X-Access-Reason': value.trim() }; }
}
