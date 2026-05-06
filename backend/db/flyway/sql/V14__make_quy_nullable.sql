-- V14: Allow QUY to be nullable to support monthly/yearly report templates.

ALTER TABLE RPT_KY_BAO_CAO MODIFY (QUY NULL);