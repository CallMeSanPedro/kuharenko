-- Проверка файла смены идентификаторов s/n, MAC и PON.
-- Рядом с verify_change_file_to_doc, отдельно от ремонта и ГСО:
-- склад подразделения ремонта, реферб и номенклатура здесь не проверяются.
--
-- MAC и PON читаются из store_device_param.mac_address и store_device_param.pon_serial.
-- Филиал документа передаётся параметром. Пустой p_filial_kod отключает проверку филиала.
-- Функция ничего не записывает в карточку. Её вызывают после загрузки файла и ещё раз при закрытии документа.
--
-- <ArrayOfIdentifierChange>
--   <IdentifierChange>
--     <store_document_id>123</store_document_id>
--     <line_no>2</line_no>
--     <serial_number>OLD</serial_number>
--     <mac_address>AABBCCDDEEFF</mac_address>
--     <pon_serial>PON1</pon_serial>
--     <new_serial_number>NEW</new_serial_number>
--     <new_mac_address></new_mac_address>
--     <new_pon_serial></new_pon_serial>
--   </IdentifierChange>
-- </ArrayOfIdentifierChange>

begin
  execute immediate 'drop function verify_identifier_change';
exception
  when others then
    if sqlcode != -4043 then
      raise;
    end if;
end;
/

begin
  execute immediate 'drop type identifier_change_tab';
exception
  when others then
    if sqlcode != -4043 then
      raise;
    end if;
end;
/

begin
  execute immediate 'drop type identifier_change_row';
exception
  when others then
    if sqlcode != -4043 then
      raise;
    end if;
end;
/

create or replace function identifier_change_norm_mac(p_value in varchar2) return varchar2 deterministic is
begin
  return nullif(upper(replace(replace(replace(replace(trim(p_value), ':', ''), '-', ''), '.', ''), ' ', '')), '');
end;
/

create or replace type identifier_change_row as object (
  store_document_id  number,
  line_no            number,
  comm_id            number,
  serial_number      varchar2(100),
  mac_address        varchar2(100),
  pon_serial         varchar2(100),
  new_serial_number  varchar2(100),
  new_mac_address    varchar2(100),
  new_pon_serial     varchar2(100),
  card_mac_address   varchar2(100),
  card_pon_serial    varchar2(100),
  filial_kod         number,
  verify_status_num  number,
  verify_status_str  varchar2(1000)
);
/

create or replace type identifier_change_tab as table of identifier_change_row;
/

create or replace function verify_identifier_change(p_store_document_id in number,
                                                     p_xml               in clob,
                                                     p_filial_kod        in number default null)
  return identifier_change_tab pipelined is
  -- Как в verify_change_file_to_doc: exploit_status_id = 5 не считается наличием на складе.
  c_status_not_on_store constant number := 5;

  v_xml          xmltype;
  v_tab          identifier_change_tab := identifier_change_tab();
  v_comm_id      number;
  v_filial       number;
  v_card_mac     varchar2(100);
  v_card_pon     varchar2(100);
  v_cnt          number;
  v_key          varchar2(200);

  type t_count_map is table of pls_integer index by varchar2(200);
  v_current_sn t_count_map;
  v_new_sn     t_count_map;
  v_new_mac    t_count_map;

  cursor v_cur(p_in_xml xmltype) is
    select identifier_change_row(
             nvl(case
                   when regexp_like(trim(xml_tab.store_document_id), '^[0-9]+$') then
                    to_number(trim(xml_tab.store_document_id))
                 end,
                 p_store_document_id),
             case
               when regexp_like(trim(xml_tab.line_no), '^[0-9]+$') then
                to_number(trim(xml_tab.line_no))
             end,
             null,
             nullif(trim(xml_tab.serial_number), ''),
             nullif(trim(xml_tab.mac_address), ''),
             nullif(trim(xml_tab.pon_serial), ''),
             nullif(trim(xml_tab.new_serial_number), ''),
             nullif(trim(xml_tab.new_mac_address), ''),
             nullif(trim(xml_tab.new_pon_serial), ''),
             null,
             null,
             null,
             null,
             null)
      from xmltable('/ArrayOfIdentifierChange/IdentifierChange' passing p_in_xml columns
                    store_document_id varchar2(50) path 'store_document_id',
                    line_no varchar2(50) path 'line_no',
                    serial_number varchar2(100) path 'serial_number',
                    mac_address varchar2(100) path 'mac_address',
                    pon_serial varchar2(100) path 'pon_serial',
                    new_serial_number varchar2(100) path 'new_serial_number',
                    new_mac_address varchar2(100) path 'new_mac_address',
                    new_pon_serial varchar2(100) path 'new_pon_serial') xml_tab;

  procedure add_error(p_index in pls_integer, p_message in varchar2) is
  begin
    v_tab(p_index).verify_status_num := -1;
    if v_tab(p_index).verify_status_str is null then
      v_tab(p_index).verify_status_str := p_message;
    elsif instr(v_tab(p_index).verify_status_str, p_message) = 0 then
      v_tab(p_index).verify_status_str := v_tab(p_index).verify_status_str || '; ' || p_message;
    end if;
  end;

  procedure inc_key(p_map in out nocopy t_count_map, p_key in varchar2) is
  begin
    if p_key is null then
      return;
    end if;
    if p_map.exists(p_key) then
      p_map(p_key) := p_map(p_key) + 1;
    else
      p_map(p_key) := 1;
    end if;
  end;

  function sn_changes(p_row in identifier_change_row) return boolean is
  begin
    if p_row.serial_number is null or p_row.new_serial_number is null then
      return false;
    end if;
    return lower(trim(p_row.new_serial_number)) <> lower(trim(p_row.serial_number));
  end;

  function mac_changes(p_row in identifier_change_row) return boolean is
    v_new varchar2(100);
    v_old varchar2(100);
  begin
    v_new := identifier_change_norm_mac(p_row.new_mac_address);
    v_old := identifier_change_norm_mac(p_row.card_mac_address);
    return v_new is not null and v_new <> nvl(v_old, chr(1));
  end;

  function pon_changes(p_row in identifier_change_row) return boolean is
  begin
    return p_row.new_pon_serial is not null
           and lower(trim(p_row.new_pon_serial)) <> nvl(lower(trim(p_row.card_pon_serial)), chr(1));
  end;

begin
  if p_xml is null then
    return;
  end if;

  begin
    v_xml := xmltype(p_xml);
  exception
    when others then
      raise_application_error(-20001, 'Неверный формат xml ' || sqlerrm);
  end;

  open v_cur(v_xml);
  fetch v_cur bulk collect into v_tab;
  close v_cur;

  if v_tab.count() = 0 then
    return;
  end if;

  for i in 1 .. v_tab.count() loop
    if v_tab(i).serial_number is null then
      add_error(i, 'Не заполнен SN_текущий');
    else
      inc_key(v_current_sn, lower(trim(v_tab(i).serial_number)));
    end if;

    if sn_changes(v_tab(i)) then
      inc_key(v_new_sn, lower(trim(v_tab(i).new_serial_number)));
    end if;

    if v_tab(i).serial_number is null then
      if v_tab(i).new_mac_address is not null then
        inc_key(v_new_mac, identifier_change_norm_mac(v_tab(i).new_mac_address));
      end if;
      continue;
    end if;

    v_comm_id := null;
    select count(distinct c.comm_id)
      into v_cnt
      from eth_comm_atu c
     where c.priznakdel = 0
       and lower(trim(c.numbdec)) = lower(trim(v_tab(i).serial_number))
       and exists (select 1
                     from store_device_param p
                    where p.comm_id = c.comm_id
                      and p.priznakdel = 0);

    if v_cnt = 0 then
      add_error(i, 'Оборудование не найдено');
    elsif v_cnt > 1 then
      add_error(i, 'Найдено больше одной карточки');
    else
      begin
        select c.comm_id,
               u.filial_kod,
               (select max(p.mac_address)
                  from store_device_param p
                 where p.comm_id = c.comm_id
                   and p.priznakdel = 0),
               (select max(p.pon_serial)
                  from store_device_param p
                 where p.comm_id = c.comm_id
                   and p.priznakdel = 0)
          into v_comm_id, v_filial, v_card_mac, v_card_pon
          from eth_comm_atu c
          left join store_infouzel u on u.infouzel_kod = c.uzel
         where c.priznakdel = 0
           and lower(trim(c.numbdec)) = lower(trim(v_tab(i).serial_number))
           and exists (select 1
                         from store_device_param p
                        where p.comm_id = c.comm_id
                          and p.priznakdel = 0);
      exception
        when no_data_found then
          add_error(i, 'Оборудование не найдено');
        when too_many_rows then
          v_comm_id := null;
          add_error(i, 'Найдено больше одной карточки');
      end;
    end if;

    if v_comm_id is null then
      if v_tab(i).new_mac_address is not null then
        inc_key(v_new_mac, identifier_change_norm_mac(v_tab(i).new_mac_address));
      end if;
      continue;
    end if;

    v_tab(i).comm_id := v_comm_id;
    v_tab(i).filial_kod := v_filial;
    v_tab(i).card_mac_address := v_card_mac;
    v_tab(i).card_pon_serial := v_card_pon;

    if v_tab(i).mac_address is not null
       and identifier_change_norm_mac(v_tab(i).mac_address) <> nvl(identifier_change_norm_mac(v_card_mac), chr(1)) then
      add_error(i, 'Контрольный MAC не совпал');
    end if;

    if v_tab(i).pon_serial is not null
       and lower(trim(v_tab(i).pon_serial)) <> nvl(lower(trim(v_card_pon)), chr(1)) then
      add_error(i, 'Контрольный PON не совпал');
    end if;

    if p_filial_kod is not null and v_filial is not null and v_filial <> p_filial_kod then
      add_error(i, 'Оборудование принадлежит к другому филиалу');
    end if;

    if not sn_changes(v_tab(i)) and not mac_changes(v_tab(i)) and not pon_changes(v_tab(i)) then
      add_error(i, 'Не задано ни одного нового значения');
    end if;

    if mac_changes(v_tab(i)) then
      inc_key(v_new_mac, identifier_change_norm_mac(v_tab(i).new_mac_address));
    end if;
  end loop;

  for i in 1 .. v_tab.count() loop
    v_key := lower(trim(v_tab(i).serial_number));
    if v_key is not null and v_current_sn.exists(v_key) and v_current_sn(v_key) > 1 then
      add_error(i, 'Повторяющийся SN_текущий');
    end if;

    if sn_changes(v_tab(i)) then
      v_key := lower(trim(v_tab(i).new_serial_number));
      if v_new_sn.exists(v_key) and v_new_sn(v_key) > 1 then
        add_error(i, 'Повторяющийся SN_новый');
      end if;
    end if;

    if v_tab(i).new_mac_address is not null then
      v_key := identifier_change_norm_mac(v_tab(i).new_mac_address);
      if v_key is not null and v_new_mac.exists(v_key) and v_new_mac(v_key) > 1 then
        add_error(i, 'Повторяющийся MAC_новый');
      end if;
    end if;
  end loop;

  for i in 1 .. v_tab.count() loop
    if v_tab(i).verify_status_num = -1 then
      continue;
    end if;

    if sn_changes(v_tab(i)) then
      select count(distinct c.comm_id)
        into v_cnt
        from eth_comm_atu c
        join store_device_param p on p.comm_id = c.comm_id and p.priznakdel = 0
       where c.priznakdel = 0
         and nvl(p.exploit_status_id, -1) <> c_status_not_on_store
         and c.comm_id <> v_tab(i).comm_id
         and lower(trim(c.numbdec)) = lower(trim(v_tab(i).new_serial_number))
         and (select count(*)
                from table(v_tab) t
               where t.comm_id = c.comm_id
                 and t.new_serial_number is not null
                 and lower(trim(t.new_serial_number)) <> lower(trim(t.serial_number))) <> 1;

      if v_cnt > 0 then
        add_error(i, 'Новый серийный номер уже есть в системе');
      end if;
    end if;

    if v_tab(i).verify_status_num = -1 then
      continue;
    end if;

    if mac_changes(v_tab(i)) then
      select count(distinct c.comm_id)
        into v_cnt
        from eth_comm_atu c
        join store_device_param p on p.comm_id = c.comm_id and p.priznakdel = 0
       where c.priznakdel = 0
         and nvl(p.exploit_status_id, -1) <> c_status_not_on_store
         and c.comm_id <> v_tab(i).comm_id
         and identifier_change_norm_mac(p.mac_address) = identifier_change_norm_mac(v_tab(i).new_mac_address)
         and (select count(*)
                from table(v_tab) t
               where t.comm_id = c.comm_id
                 and identifier_change_norm_mac(t.new_mac_address) <> nvl(identifier_change_norm_mac(t.card_mac_address), chr(1))) <> 1;

      if v_cnt > 0 then
        add_error(i, 'Новый MAC уже есть в системе');
      end if;
    end if;

    if v_tab(i).verify_status_num = -1 then
      continue;
    end if;

    v_tab(i).verify_status_num := 1;
    v_tab(i).verify_status_str := nvl(v_tab(i).verify_status_str, 'Успешно');
  end loop;

  for i in 1 .. v_tab.count() loop
    pipe row(v_tab(i));
  end loop;

  return;
end;
/
