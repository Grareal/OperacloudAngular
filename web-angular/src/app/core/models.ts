// DTOs del contrato HTTP (camelCase). Los nuevos contratos se generan desde OpenAPI.
export interface ReservationIdInfo { id: string; type: string }
export interface GuestAddress { city: string; stateProvCode: string; countryCode: string }
export interface ReservationGuest {
  id?: string; fullName: string; givenName: string; surname: string;
  email: string; phoneNumber: string; language: string; address: GuestAddress;
}
export interface RoomStay {
  roomId: string; roomType: string; roomClass: string; arrivalDate: string; departureDate: string;
  adultCount: number; childCount: number; rateAmount: number; currencyCode: string;
  ratePlanCode: string; guaranteeCode: string; guaranteeDescription: string;
}
export interface UserDefinedField { name: string; value: string }
export interface AccompanyingGuest { fullName: string; profileId?: string; reservationGuestId?: string }
export interface Reservation {
  confirmationNumber: string; hotelId: string; reservationStatus: string;
  reservationIdList: ReservationIdInfo[];
  guest: ReservationGuest; roomStay: RoomStay; userDefinedFields: UserDefinedField[];
  tswNumber?: string; accompanyingGuests: AccompanyingGuest[]; accompanyingGuestNames: string[];
  companions: { name: string; profileId?: string }[];
}

export interface AuthSession {
  username: string; displayName: string; role: string; permissions: string[];
}
export interface ApiEnvironmentInfo { environment: string; isUat: boolean; hotelId: string; database: string; gateway: string }

export interface OfficialCardOccupantInput { signerId?: string; name: string; signaturePngBase64: string; selected: boolean }
export interface OfficialCardInput {
  template?: string; localTemplateId?: string; citizenship?: string; city?: string; state?: string;
  country?: string; email?: string; cellPhone?: string; primaryGuestName?: string; primarySignerId?: string;
  primarySignaturePngBase64?: string; marketingConsent: boolean; occupants: OfficialCardOccupantInput[];
}
export interface OperaAttachmentResponse {
  attachmentId: string; fileName: string; fileSize: number; description?: string; template: string;
  documentHash: string; localDocumentId?: string; localDocumentVersion?: number;
  emailDeliveryId?: string; emailStatus?: string;
}

export interface StoredSignatureInfo {
  id: string; confirmationNumber: string; roomNumber?: string; signerName: string; signerKey: string;
  operaProfileId?: string; signerRole: string; signatureHash: string; signedAtUtc: string;
  localDocumentId?: string; documentVersion?: number; attachmentId?: string;
}
export interface ReservationLocalDocumentInfo {
  id: string; reservationFileId?: string; hotelId: string; confirmationNumber: string; roomNumber?: string; version: number;
  fileName: string; documentHash: string; status: string; attachmentId?: string; attachmentFileName?: string;
  createdAtUtc: string; uploadedAtUtc?: string; signatureCount: number;
}
export interface ReservationFileInfo {
  id: string; hotelId: string; confirmationNumber: string; reservationId?: string; version: number;
  status: string; manifestHash: string; createdAtUtc: string; sealedAtUtc?: string;
  createdBy?: string; sealedBy?: string; documentCount: number;
}
export interface ReservationSignatureInfo {
  id: string; signerName: string; signerKey: string; operaProfileId?: string; signerRole: string;
  signatureHash: string; signedAtUtc: string; roomNumber?: string;
}
export interface ReservationEmailItemInfo { id: string; documentType: string; name: string; version: number; fileName: string; contentType: string; documentHash: string }
export interface ReservationEmailDeliveryInfo {
  id: string; guestName: string; recipientEmail: string; language: string; marketingConsent: boolean;
  status: string; attemptCount: number; createdAtUtc: string; sentAtUtc?: string; lastError?: string; items: ReservationEmailItemInfo[];
}
export interface ReservationEmailPreview { fromName: string; fromAddress: string; recipient: string; subject: string; bodyHtml: string; attachments: ReservationEmailItemInfo[] }
export interface ReservationDocumentPackage {
  confirmationNumber: string; found: boolean; guestName?: string; roomNumber?: string;
  files: ReservationFileInfo[]; documents: ReservationLocalDocumentInfo[]; signatures: ReservationSignatureInfo[];
  deliveries: ReservationEmailDeliveryInfo[]; emailPreview: ReservationEmailPreview;
}

export interface PdfTemplateSummary { id: string; hotelId: string; name: string; templateType: string; version: number; fileName: string; isPublished: boolean; isDefault: boolean; roomTypePrefix?: string; language?: string; createdAtUtc: string; fieldCount: number }
export interface PdfFieldCatalogItem { key: string; label: string; type: string }
export interface PdfTemplateFieldInfo { id: string; fieldKey: string; label: string; fieldType: string; pageNumber: number; xPercent: number; yPercent: number; widthPercent: number; heightPercent: number; fontSize: number; occupantIndex?: number }
export interface PdfTemplateDetail { id: string; hotelId: string; name: string; templateType: string; version: number; fileName: string; isPublished: boolean; isDefault: boolean; roomTypePrefix?: string; language?: string; fields: PdfTemplateFieldInfo[] }
export interface HotelDocumentSetting { hotelId: string; sourceMode: string; preferredPdfTemplateId?: string }

export interface CommunicationDocumentInfo {
  id: string; hotelId: string; type: string; name: string; language: string; version: number;
  source: string; sourceUrl?: string; fileName: string; contentType: string; fileSize: number; documentHash?: string;
  isPublished: boolean; isRequired: boolean; requiresMarketingConsent: boolean; sortOrder: number;
  effectiveFromUtc?: string; effectiveToUtc?: string; createdAtUtc: string;
}
export interface GuestEmailDeliveryInfo { id: string; confirmationNumber: string; guestName: string; recipientEmail: string; status: string; attemptCount: number; marketingConsent: boolean; createdAtUtc: string; sentAtUtc?: string; lastError?: string; itemCount: number }
export interface GuestEmailSettingInfo {
  hotelId: string; enabled: boolean; smtpConfigured: boolean; host: string; port: number; enableSsl: boolean;
  username: string; password: string; passwordConfigured: boolean; fromAddress: string; fromName: string;
  subject: string; bodyHtml: string; maxAttempts: number;
}

export interface PromotionCatalogInfo {
  id: string; hotelId: string; operaCode: string; operaDescription: string; guestTitle: string; guestDescription: string;
  language: string; isActive: boolean; isApprovedForGuest: boolean; sortOrder: number;
  effectiveFromUtc?: string; effectiveToUtc?: string; source: string; createdAtUtc: string; updatedAtUtc: string; updatedBy?: string;
}
export interface OperaPromotionRateInfo { code: string; description: string }
export interface OperaPromotionCodeInfo {
  code: string; name: string; groupCode: string; groupName: string; bookingStartDate: string; bookingEndDate: string;
  stayStartDate: string; stayEndDate: string; description: string; hotelId: string; rates: OperaPromotionRateInfo[];
}

export interface OperaGuestProfileInfo { profileId: string; fullName: string; primary: boolean }
export interface AccompanyingGuestChangePreview {
  hotelId: string; confirmationNumber: string; reservationId: string; reservationStatus: string; lastModifyDateTime: string;
  currentAdults: number; proposedAdults: number; children: number; requestedProfile: OperaGuestProfileInfo;
  currentGuests: OperaGuestProfileInfo[]; proposedGuests: OperaGuestProfileInfo[];
  alreadyLinked: boolean; canApply: boolean; validationMessage: string;
}
export interface AccompanyingGuestChangeResult { auditId: string; correlationId: string; before: AccompanyingGuestChangePreview; after: AccompanyingGuestChangePreview; operaResponse: string }

export interface OcrIdentityFields { docType?: string; fullName?: string; curp?: string; claveElector?: string; vigencia?: string; passportNumber?: string; mrzLine1?: string; mrzLine2?: string }
export interface OcrParseResult {
  docType: string; frontConfidence: number; backConfidence?: number; frontText: string; backText?: string;
  fields: OcrIdentityFields; fieldConfidences: Record<string, number>; warnings: string[]; humanReviewRequired: boolean;
}

export interface PermissionInfo { key: string; label: string }
export interface UserGroupInfo { id: number; name: string; description: string; permissions: string[]; isSystem: boolean; isActive: boolean }
export interface AppUserInfo { id: number; username: string; displayName: string; role: string; userGroupId?: number; isActive: boolean; createdAt: string }
export interface AccessAdministration { permissions: PermissionInfo[]; groups: UserGroupInfo[]; users: AppUserInfo[] }

export const ViewPermissions = {
  Operation: 'operation', RegistrationCard: 'registration-card', SignatureLookup: 'signature-lookup',
  History: 'history', Documents: 'documents', PdfTemplates: 'pdf-templates', Communications: 'communications',
  EmailSettings: 'email-settings', Promotions: 'promotions', AccompanyingGuests: 'accompanying-guests',
  UserAdministration: 'user-administration',
  Audit: 'audit', OcrIdentity: 'ocr-identity',
} as const;
