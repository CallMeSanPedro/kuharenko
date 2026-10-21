-- Добавление строк смены идентификаторов в документ.
-- XML тот же, что у verify_identifier_change. Перед записью строки проверяются заново,
-- в документ попадают только строки со статусом 1. Повторная загрузка той же карточки заменяет строку.

create or replace procedure add_identifier_change_to_doc(p_store_document_id in number,
                                                         p_xml               in clob,
                                                         p_ownership_id      in number default null,
                                                         p_dev_condition_id  in number default null,
                                                         p_sosttech_kod      in number default null) is
  type t_row is record(
    store_document_id number,
    line_no           number,
    comm_id           number,
    serial_number     varchar2(100),
    mac_address       varchar2(100),
    pon_serial        varchar2(100),
    new_serial_number varchar2(100),
    new_mac_address   varchar2(100),
    new_pon_serial    varchar2(100),
    card_mac_address  varchar2(100),
    card_pon_serial   varchar2(100),
    filial_kod        number,
    verify_status_num number,
    verify_status_str varchar2(1000));

  v_rc               sys_refcursor;
  v_row              t_row;
  v_added            pls_integer := 0;
  v_parameter_change number := 0;
begin
  if p_store_document_id is null then
    raise_application_error(-20001, 'Не указан документ');
  end if;

  v_rc := verify_identifier_change(p_store_document_id, p_xml);

  begin
    select nvl(max(case
                     when trim(x.parameter_change) = '1' then
                      1
                     else
                      0
                   end),
               0)
      into v_parameter_change
      from xmltable('/ArrayOfIdentifierChange/IdentifierChange' passing xmltype(p_xml) columns
                    parameter_change varchar2(10) path 'parameter_change') x;
  exception
    when others then
      v_parameter_change := 0;
  end;

  loop
    fetch v_rc
      into v_row;
    exit when v_rc%notfound;

    if v_row.verify_status_num = 1 then
      delete from store_doc_identifier_change d
       where d.store_document_id = p_store_document_id
         and d.comm_id = v_row.comm_id
         and d.applied_date is null;

      insert into store_doc_identifier_change
        (line_id,
         store_document_id,
         comm_id,
         serial_number,
         mac_address,
         pon_serial,
         new_serial_number,
         new_mac_address,
         new_pon_serial,
         card_mac_address,
         card_pon_serial,
         ownership_id,
         dev_condition_id,
         sosttech_kod,
         parameter_change,
         verify_status_str)
      values
        (store_doc_identifier_change_seq.nextval,
         p_store_document_id,
         v_row.comm_id,
         v_row.serial_number,
         v_row.mac_address,
         v_row.pon_serial,
         v_row.new_serial_number,
         v_row.new_mac_address,
         v_row.new_pon_serial,
         v_row.card_mac_address,
         v_row.card_pon_serial,
         case when v_parameter_change = 1 then p_ownership_id end,
         case when v_parameter_change = 1 then p_dev_condition_id end,
         case when v_parameter_change = 1 then p_sosttech_kod end,
         v_parameter_change,
         v_row.verify_status_str);

      v_added := v_added + 1;
    end if;
  end loop;
  close v_rc;

  if v_added = 0 then
    raise_application_error(-20002, 'Нет строк, прошедших проверку');
  end if;
exception
  when others then
    if v_rc%isopen then
      close v_rc;
    end if;
    raise;
end;
/
