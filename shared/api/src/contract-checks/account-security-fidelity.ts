// M8 Phases 2–5: additive caller identity, safe account/capability responses and request lifecycle fidelity.
import type {
  InvitationStatus,
  MailDeliveryStatus,
  OperatorLinkRequestStatus,
  RequestReissueInvitationJson,
  ResponseAcceptInvitationJson,
  ResponseInvitationPreviewJson,
  ResponseListInvitationsJson,
  ResponseOperatorLinkRequestJson,
  ResponseOwnOperatorLinkRequestJson,
  ResponseUserLoginJson,
  ResponseUserProfileJson,
} from '../index';
import type { Equal, Expect, IsNullable, IsOptional } from './assert';

export type AccountSecurityFidelityChecks = [
  Expect<Equal<IsOptional<RequestReissueInvitationJson, 'operatorId'>, true>>,
  Expect<Equal<IsNullable<RequestReissueInvitationJson['operatorId']>, true>>,
  Expect<Equal<IsOptional<ResponseUserLoginJson, 'userId'>, false>>,
  Expect<Equal<ResponseUserLoginJson['userId'], string>>,
  Expect<
    Equal<
      keyof ResponseUserProfileJson,
      'userId' | 'name' | 'email' | 'isPlatformOperator' | 'hasPassword'
    >
  >,
  Expect<Equal<ResponseUserProfileJson['isPlatformOperator'], boolean>>,
  Expect<Equal<ResponseUserProfileJson['hasPassword'], boolean>>,
  Expect<
    Equal<keyof ResponseInvitationPreviewJson, 'branchName' | 'role' | 'expiresAt' | 'operatorName'>
  >,
  Expect<Equal<InvitationStatus, 'Pending' | 'Accepted' | 'Declined' | 'Expired'>>,
  Expect<Equal<OperatorLinkRequestStatus, 'Pending' | 'Resolved'>>,
  Expect<Equal<MailDeliveryStatus, 'Pending' | 'Sent' | 'Failed'>>,
  Expect<Equal<IsOptional<ResponseOwnOperatorLinkRequestJson, 'request'>, true>>,
  Expect<Equal<IsNullable<ResponseOwnOperatorLinkRequestJson['request']>, true>>,
  Expect<Equal<IsOptional<ResponseOperatorLinkRequestJson, 'hasUsableAccount'>, false>>,
  Expect<Equal<ResponseOperatorLinkRequestJson['hasUsableAccount'], boolean>>,
  Expect<Equal<ResponseListInvitationsJson['totalCount'], number>>,
  Expect<Equal<IsOptional<ResponseAcceptInvitationJson, 'operatorId'>, true>>,
  Expect<Equal<IsNullable<ResponseAcceptInvitationJson['operatorId']>, true>>,
];
