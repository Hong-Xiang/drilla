(module
  (func (export "run") (param i32) (result i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32) (local i32)
    i32.const 0
    local.set 1
    loop
      local.get 1
      i32.const 0
      i32.eq
      if
        local.get 0
        local.set 2
        i32.const 0
        local.set 13
        i32.const 0
        local.set 14
        local.get 2
        local.set 15
        local.get 13
        local.set 3
        local.get 14
        local.set 4
        local.get 15
        local.set 5
        i32.const 1
        local.set 1
        br 1
      end
      local.get 1
      i32.const 1
      i32.eq
      if
        local.get 4
        local.get 5
        i32.lt_s
        local.set 6
        local.get 6
        if
          local.get 3
          local.set 13
          local.get 4
          local.set 14
          local.get 5
          local.set 15
          local.get 13
          local.set 7
          local.get 14
          local.set 8
          local.get 15
          local.set 9
          i32.const 2
          local.set 1
          br 2
        else
          local.get 3
          local.set 13
          local.get 13
          local.set 12
          i32.const 3
          local.set 1
          br 2
        end
      end
      local.get 1
      i32.const 2
      i32.eq
      if
        local.get 7
        local.get 8
        i32.add
        local.set 10
        local.get 8
        i32.const 1
        i32.add
        local.set 11
        local.get 10
        local.set 13
        local.get 11
        local.set 14
        local.get 9
        local.set 15
        local.get 13
        local.set 3
        local.get 14
        local.set 4
        local.get 15
        local.set 5
        i32.const 1
        local.set 1
        br 1
      end
      local.get 1
      i32.const 3
      i32.eq
      if
        local.get 12
        return
      end
      unreachable
    end
    unreachable
  )
)
