/**
 * A group as an ordinary caller sees it (`GroupSummaryDto`): a name to aim a link at, and nothing
 * about who else is in it. The admin area has its own, fuller model.
 */
export interface IGroupSummary {
  id: string;

  name: string;
}
