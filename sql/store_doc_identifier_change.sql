-- Строки документа корректировки идентификаторов: что было и что станет.
-- Заполняется add_identifier_change_to_doc, применяется apply_identifier_change_doc при закрытии документа.

create sequence store_doc_identifier_change_seq;

create table store_doc_identifier_change
(
  line_id           number not null,
  store_document_id number not null,
  comm_id           number not null,
  serial_number     varchar2(100),
  mac_address       varchar2(100),
  pon_serial        varchar2(100),
  new_serial_number varchar2(100),
  new_mac_address   varchar2(100),
  new_pon_serial    varchar2(100),
  card_mac_address  varchar2(100),
  card_pon_serial   varchar2(100),
  ownership_id      number,
  dev_condition_id  number,
  sosttech_kod      number,
  parameter_change  number default 0 not null,
  verify_status_str varchar2(1000),
  create_date       date default sysdate not null,
  applied_date      date,
  constraint store_doc_identifier_change_pk primary key (line_id)
);

create unique index store_doc_identifier_change_u1 on store_doc_identifier_change (store_document_id, comm_id);

comment on table store_doc_identifier_change is 'Смена s/n, MAC и PON по документу корректировки';
comment on column store_doc_identifier_change.serial_number is 'SN на момент загрузки';
comment on column store_doc_identifier_change.new_serial_number is 'Новый SN, пусто — не менять';
comment on column store_doc_identifier_change.new_mac_address is 'Новый MAC, пусто — не менять';
comment on column store_doc_identifier_change.new_pon_serial is 'Новый PON, пусто — не менять';
comment on column store_doc_identifier_change.applied_date is 'Когда значения записаны на карточку';
comment on column store_doc_identifier_change.ownership_id is 'Форма владения для всех строк загрузки, пусто — не менять';
comment on column store_doc_identifier_change.dev_condition_id is 'Состояние для всех строк загрузки, пусто — не менять';
comment on column store_doc_identifier_change.sosttech_kod is 'Техническое состояние для всех строк загрузки, пусто — не менять';
comment on column store_doc_identifier_change.parameter_change is '1 — смена параметров; 0 — смена идентификаторов';

-- Если таблица уже создана без этих колонок:
-- alter table store_doc_identifier_change add (
--   ownership_id number,
--   dev_condition_id number,
--   sosttech_kod number,
--   parameter_change number default 0
-- );
