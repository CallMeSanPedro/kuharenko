-- Закрытие документа корректировки: статусы и параметры оборудования
-- берутся из XML, закреплённого за документом.
-- Вызывать из процедуры закрытия до смены статуса документа:
--   v_cnt := close_identifier_change_doc(p_store_document_id);
-- Источник — store_documents.xml_file, корень ArrayOfIdentifierChangeResult.
-- Пустое новое значение заменяется текущим, чтобы lite-процедура не очищала поле.
-- p_exploit_status_id и p_recovery_status_id остаются выключенными:
-- в XML документа этих статусов нет.

create or replace function close_identifier_change_doc(p_store_document_id in number) return number is
  v_xml clob;
  v_ret number;
  v_cnt pls_integer := 0;
begin
  if p_store_document_id is null then
    raise_application_error(-20001, 'Не указан документ');
  end if;

  begin
    select d.xml_file
      into v_xml
      from store_documents d
     where d.store_document_id = p_store_document_id;
  exception
    when no_data_found then
      raise_application_error(-20004, 'Документ не найден');
  end;

  if v_xml is null then
    raise_application_error(-20005, 'На документе нет xml');
  end if;

  for v_rec in (select distinct p.comm_id,
                                nullif(trim(xml_tab.serial_number), '') serial_number,
                                nullif(trim(xml_tab.new_serial_number), '') new_serial_number,
                                nullif(trim(xml_tab.mac_address), '') mac_address,
                                nullif(trim(xml_tab.new_mac_address), '') new_mac_address,
                                nullif(trim(xml_tab.card_mac_address), '') card_mac_address,
                                nullif(trim(xml_tab.pon_serial), '') pon_serial,
                                nullif(trim(xml_tab.new_pon_serial), '') new_pon_serial,
                                nullif(trim(xml_tab.card_pon_serial), '') card_pon_serial,
                                case
                                  when regexp_like(trim(xml_tab.ownership_id), '^[0-9]+$') then
                                   to_number(trim(xml_tab.ownership_id))
                                end ownership_id,
                                case
                                  when regexp_like(trim(xml_tab.dev_condition_id), '^[0-9]+$') then
                                   to_number(trim(xml_tab.dev_condition_id))
                                end dev_condition_id,
                                case
                                  when regexp_like(trim(xml_tab.sosttech_kod), '^[0-9]+$') then
                                   to_number(trim(xml_tab.sosttech_kod))
                                end sosttech_kod
                  from xmltable('/ArrayOfIdentifierChangeResult/IdentifierChangeResult'
                                passing (select xmltype(d.xml_file)
                                           from store_documents d
                                          where d.store_document_id = p_store_document_id)
                                columns serial_number varchar2(100) path 'serial_number',
                                        new_serial_number varchar2(100) path 'new_serial_number',
                                        mac_address varchar2(100) path 'mac_address',
                                        new_mac_address varchar2(100) path 'new_mac_address',
                                        card_mac_address varchar2(100) path 'card_mac_address',
                                        pon_serial varchar2(100) path 'pon_serial',
                                        new_pon_serial varchar2(100) path 'new_pon_serial',
                                        card_pon_serial varchar2(100) path 'card_pon_serial',
                                        comm_id number path 'comm_id',
                                        ownership_id varchar2(50) path 'ownership_id',
                                        dev_condition_id varchar2(50) path 'dev_condition_id',
                                        sosttech_kod varchar2(50) path 'sosttech_kod',
                                        verify_status_num number path 'verify_status_num') xml_tab
                  join eth_comm_atu c
                    on lower(trim(c.numbdec)) = lower(trim(xml_tab.serial_number))
                   and c.priznakdel = 0
                   and c.comm_id = xml_tab.comm_id
                  join store_device_param p
                    on p.comm_id = c.comm_id
                   and p.priznakdel = 0
                 where xml_tab.verify_status_num = 1) loop
    v_ret := store_utils.store_device_update_lite(p_eth_comm_atu_id => v_rec.comm_id,
                                                   p_eth_serial_numb => nvl(v_rec.new_serial_number, v_rec.serial_number),
                                                   p_eth_mac_address => nvl(v_rec.new_mac_address,
                                                                            nvl(v_rec.card_mac_address, v_rec.mac_address)),
                                                   p_pon_serial      => nvl(v_rec.new_pon_serial,
                                                                            nvl(v_rec.card_pon_serial, v_rec.pon_serial)),
                                                   p_ownership_id    => v_rec.ownership_id,
                                                   p_sosttech_kod    => v_rec.sosttech_kod
                                                   -- p_exploit_status_id => ltu_atua_store.c_exploit_on_work,
                                                   -- p_recovery_status_id => c_recover_off
                                                   );

    if v_rec.dev_condition_id is not null then
      update store_device_param p
         set p.dev_condition_id = v_rec.dev_condition_id
       where p.comm_id = v_rec.comm_id
         and p.priznakdel = 0;
    end if;

    update store_doc_identifier_change d
       set d.applied_date      = sysdate,
           d.verify_status_str = 'Применено'
     where d.store_document_id = p_store_document_id
       and d.comm_id = v_rec.comm_id
       and d.applied_date is null;

    v_cnt := v_cnt + 1;
  end loop;

  return v_cnt;
end;
/
