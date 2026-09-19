(module
  (func (export "run") (param i32) (param i32) (param i32) (result i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32)
    i32.const 0
    local.set 3
    loop
      local.get 3
      i32.const 0
      i32.eq
      if
        local.get 0
        local.set 4
        local.get 4
        i32.const 0
        i32.ne
        local.set 5
        local.get 1
        local.set 6
        local.get 2
        local.set 7
        local.get 5
        if
          local.get 6
          local.set 9
          local.get 9
          local.set 8
          i32.const 1
          local.set 3
          br 2
        else
          local.get 7
          local.set 9
          local.get 9
          local.set 8
          i32.const 1
          local.set 3
          br 2
        end
      end
      local.get 3
      i32.const 1
      i32.eq
      if
        local.get 8
        return
      end
      unreachable
    end
    unreachable
  )
)
