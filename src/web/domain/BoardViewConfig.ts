import { FlyOutForm } from "./FlyOutForm";
import { EntityReference } from "xrm-webapi-client";

export interface CustomButton {
    id: string;
    icon: { type: string; value: string; };
    label: string;
    callBack: string;
}

export interface BoardEntity {
    logicalName: string;
    swimLaneSource: string;
    hiddenLanes: Array<number>;
    visibleLanes: Array<number>;
    hiddenViews: Array<string>;
    visibleViews: Array<string>;
    emailSubscriptionsEnabled: boolean;
    emailNotificationsSender: { Id: string; LogicalName: string; };
    styleCallback: string;
    transitionCallback: string;
    notificationLookup: string;
    subscriptionLookup: string;
    preventTransitions: boolean;
    defaultView: string;
    customButtons: Array<CustomButton>;
    fitLanesToScreenWidth: boolean;
    hideCountOnLane: boolean;
    defaultOpenHandler: "inline" | "sidebyside" | "modal" | "newwindow";
}

export interface SecondaryEntity extends BoardEntity {
    parentLookup: string;
}

export interface Context {
    showForm: (form: FlyOutForm) => Promise<any>;
}

export interface PrimaryEntity extends BoardEntity {

}

export interface BoardViewConfig {
    primaryEntity: PrimaryEntity;
    secondaryEntity: SecondaryEntity;
    customScriptUrl: string;
}