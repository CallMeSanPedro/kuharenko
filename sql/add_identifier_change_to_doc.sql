-- Добавление строк смены идентификаторов в документ.
-- XML тот же, что у verify_identifier_change. Перед записью строки проверяются заново,
-- в документ попадают только строки со статусом 1. Повторная загрузка той же карточки заменяет строку.
-- Успешные строки записываются в store_documents.xml_file и привязываются к документу
-- через ltu_atua_store.storedoc_dev_link_add.

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
  v_doc_xml          clob;
  v_ret              number;

  function esc(p_value in varchar2) return varchar2 is
  begin
    return replace(replace(replace(replace(p_value, '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), '"', '&quot;');
  end;

  procedure tag(p_name in varchar2, p_value in varchar2) is
  begin
    v_doc_xml := v_doc_xml || '<' || p_name || '>' || esc(p_value) || '</' || p_name || '>';
  end;
begin
  if p_store_document_id is null then
    raise_application_error(-20001, 'Не указан документ');
  end if;

  v_doc_xml := '<ArrayOfIdentifierChangeResult>';
  v_rc      := verify_identifier_change(p_store_document_id, p_xml);

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

      v_doc_xml := v_doc_xml || '<IdentifierChangeResult>';
      tag('store_document_id', to_char(p_store_document_id));
      tag('line_no', to_char(v_row.line_no));
      tag('comm_id', to_char(v_row.comm_id));
      tag('serial_number', v_row.serial_number);
      tag('mac_address', v_row.mac_address);
      tag('pon_serial', v_row.pon_serial);
      tag('new_serial_number', v_row.new_serial_number);
      tag('new_mac_address', v_row.new_mac_address);
      tag('new_pon_serial', v_row.new_pon_serial);
      tag('card_mac_address', v_row.card_mac_address);
      tag('card_pon_serial', v_row.card_pon_serial);
      tag('filial_kod', to_char(v_row.filial_kod));
      tag('verify_status_num', '1');
      tag('verify_status_str', v_row.verify_status_str);
      if v_parameter_change = 1 then
        tag('parameter_change', '1');
        tag('ownership_id', to_char(p_ownership_id));
        tag('dev_condition_id', to_char(p_dev_condition_id));
        tag('sosttech_kod', to_char(p_sosttech_kod));
      end if;
      v_doc_xml := v_doc_xml || '</IdentifierChangeResult>';

      v_added := v_added + 1;
    end if;
  end loop;
  close v_rc;

  if v_added = 0 then
    raise_application_error(-20002, 'Нет строк, прошедших проверку');
  end if;

  v_doc_xml := v_doc_xml || '</ArrayOfIdentifierChangeResult>';

  update store_documents d
     set d.xml_file = v_doc_xml
   where d.store_document_id = p_store_document_id;

  if sql%rowcount = 0 then
    raise_application_error(-20004, 'Документ не найден');
  end if;

  for v_rec in (select distinct xml_tab.serial_number, p.comm_id
                  from xmltable('/ArrayOfIdentifierChangeResult/IdentifierChangeResult'
                                passing (select xmltype(d.xml_file)
                                           from store_documents d
                                          where d.store_document_id = p_store_document_id)
                                columns serial_number varchar2(100) path 'serial_number',
                                        comm_id number path 'comm_id',
                                        verify_status_num number path 'verify_status_num') xml_tab
                  join eth_comm_atu c
                    on lower(trim(c.numbdec)) = lower(trim(xml_tab.serial_number))
                   and c.priznakdel = 0
                   and c.comm_id = xml_tab.comm_id
                  join store_device_param p
                    on p.comm_id = c.comm_id
                   and p.priznakdel = 0
                 where xml_tab.verify_status_num = 1) loop
    v_ret := ltu_atua_store.storedoc_dev_link_add(p_eth_comm_atu_id   => v_rec.comm_id,
                                                   p_store_document_id => p_store_document_id);
  end loop;
exception
  when others then
    if v_rc%isopen then
      close v_rc;
    end if;
    raise;
end;
/
